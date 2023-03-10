using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;

namespace TokenBasedScript.Controllers;

[Authorize(Policy = "LoggedIn")]
public class CheckoutController : Controller
{
    private readonly IConfiguration _config;
    private readonly MvcContext _context;
    private readonly IGiveUser _giveUser;

    public CheckoutController(MvcContext context, IGiveUser giveUser, IConfiguration config)
    {
        _context = context;
        _giveUser = giveUser;
        _config = config;
    }

    private string StripeWebhookSecret => _config.GetValue<string>("Stripe:Webhook:Secret");

    [Authorize(Policy = "LoggedIn")]
    public Task<IActionResult> IndexAsync()
    {
        return Task.FromResult<IActionResult>(View("Index"));
    }

    [HttpPost("/{controller}")]
    [Authorize(Policy = "LoggedIn")]
    public async Task<IActionResult> Checkout([FromForm] int amount, string? promotionCode)
    {
        User? user = await _giveUser.GetUser();
        if (user == null) return RedirectToAction("Index", "Home");
        var clientReferenceId = user.Snowflake;
        var domain = Request.Scheme + "://" + Request.Host.Value + "/";
        List<SessionDiscountOptions>? discounts = null;
        if (promotionCode != null)
        {
            //resolve to id
            var promotionCodeListOptions = new PromotionCodeListOptions
            {
                Limit = 1,
                Code = promotionCode,
            };
            var promotionCodeService = new PromotionCodeService();
            var promotionCodes = await promotionCodeService.ListAsync(promotionCodeListOptions);
            if (promotionCodes.Data.Count == 0)
            {
                ViewData["ErrorMessage"] = "Invalid promotion code.";
                return View("Index");
            }

            promotionCode = promotionCodes.Data[0].Id;
            discounts = new List<SessionDiscountOptions>
            {
                new SessionDiscountOptions
                {
                    PromotionCode = promotionCode,
                }
            };
        }

        var options = new SessionCreateOptions
        {
            ClientReferenceId = clientReferenceId,
            CustomerEmail = user.Email,
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    // Provide the exact Price ID (for example, pr_1234) of the product you want to sell
                    Price = _config.GetValue<string>("Stripe:Price:ID"),
                    Quantity = amount,
                },
            },
            Discounts = discounts,
            Mode = "payment",
            SuccessUrl = domain,
            CancelUrl = domain,
        };
        var service = new SessionService();
        try
        {
            var session = await service.CreateAsync(options);

            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }
        catch (StripeException e)
        {
            ViewData["ErrorMessage"] = e.Message;
            return View("Index");
        }
    }

    //stripe listen --forward-to http://localhost:5192/api/stripe/webhook
    [HttpPost("/api/stripe/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                StripeWebhookSecret
            );
            // Handle the checkout.session.completed event
            if (stripeEvent.Type == Events.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                var options = new SessionGetOptions();
                options.AddExpand("line_items");

                var service = new SessionService();
                // Retrieve the session. If you require line items in the response, you may include them by expanding line_items.
                Session sessionWithLineItems = service.Get(session.Id, options);
                StripeList<LineItem> lineItems = sessionWithLineItems.LineItems;
                var clientReferenceId = session.ClientReferenceId;
                if (clientReferenceId == null)
                    return BadRequest(
                        "Client reference id is null. This is probably because the user is not logged in.");
                // Fulfill the purchase...
                await this.FulfillOrder(lineItems, clientReferenceId);
            }

            return Ok();
        }
        catch (StripeException e)
        {
            return BadRequest();
        }
    }

    private async Task FulfillOrder(StripeList<LineItem> lineItems, string clientReferenceId)
    {
        //find by snowflake
        var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            foreach (var item in lineItems)
            {
                if (item.Description != "Token") continue;

                await _context.Users.Where(x => x.Snowflake == clientReferenceId)
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(user => user.TokenLeft, user => user.TokenLeft + item.Quantity)
                    );

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw e;
        }
    }
}