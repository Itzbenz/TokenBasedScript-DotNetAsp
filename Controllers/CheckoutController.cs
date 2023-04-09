using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;
using Settings = TokenBasedScript.Services.Settings;

namespace TokenBasedScript.Controllers;

[Authorize(Policy = "LoggedIn")]
public class CheckoutController : Controller
{
    private readonly MvcContext _context;
    private readonly IGiveUser _giveUser;
    private readonly IAppConfigService _appConfigService;

    public CheckoutController(MvcContext context, IGiveUser giveUser, IAppConfigService appConfigService)
    {
        _context = context;
        _giveUser = giveUser;
        _appConfigService = appConfigService;
    }

    private string StripeWebhookSecret => _appConfigService.Get<string>(Settings.StripeWebhookSecret, null)!;

    [Authorize(Policy = "LoggedIn")]
    public Task<IActionResult> IndexAsync()
    {
        ViewBag.License = false;
        return Task.FromResult<IActionResult>(View("Index"));
    }

    [HttpPost("/{controller}")]
    [Authorize(Policy = "LoggedIn")]
    public async Task<IActionResult> Checkout(bool isLicense, string? promotionCode, int amount = 1)
    {
        if (isLicense) amount = 1;

        var user = await _giveUser.GetUser();
        if (user == null) return RedirectToAction("Index", "Home");
        var clientReferenceId = user.Snowflake;
        var domain = Request.Scheme + "://" + Request.Host.Value + "/";
        List<SessionDiscountOptions>? discounts = null;
        //set API key
        StripeConfiguration.ApiKey = _appConfigService.Get(Settings.StripeApiSecret, "");
        if (promotionCode != null)
        {
            //resolve to id
            var promotionCodeListOptions = new PromotionCodeListOptions
            {
                Limit = 1,
                Code = promotionCode
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
                new()
                {
                    PromotionCode = promotionCode
                }
            };
        }

        //get stripe customer or create
        var customerService = new CustomerService();
        Customer? customer = null;
        if (user.StripeCustomerId != null) customer = await customerService.GetAsync(user.StripeCustomerId);

        if (customer == null)
        {
            var customerCreateOptions = new CustomerCreateOptions
            {
                Email = user.Email,
                Metadata = new Dictionary<string, string>
                {
                    {"Snowflake", user.Snowflake ?? string.Empty},
                    {"UserId", user.Id}
                }
            };
            customer = await customerService.CreateAsync(customerCreateOptions);
            user.StripeCustomerId = customer.Id;
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        var options = new SessionCreateOptions
        {
            ClientReferenceId = clientReferenceId,
            Customer = customer.Id,
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    // Provide the exact Price ID (for example, pr_1234) of the product you want to sell
                    Price = _appConfigService.Get(
                        isLicense ? Settings.StripePriceIdForLicense : Settings.StripePriceIdForToken, ""),
                    Quantity = amount
                }
            },
            Discounts = discounts,
            Mode = isLicense ? "subscription" : "payment",

            SuccessUrl = domain,
            CancelUrl = domain
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
            ViewBag.License = isLicense;
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
                if (stripeEvent.Data.Object is not Session session) return Ok();
                var options = new SessionGetOptions();
                options.AddExpand("line_items");

                var service = new SessionService();
                // Retrieve the session. If you require line items in the response, you may include them by expanding line_items.
                var sessionWithLineItems = await service.GetAsync(session.Id, options);
                StripeList<LineItem> lineItems = sessionWithLineItems.LineItems;
                var clientReferenceId = session.ClientReferenceId;
                if (clientReferenceId == null)
                    return BadRequest(
                        "Client reference id is null. This is probably because the user is not logged in.");
                // Fulfill the purchase...
                await FulfillOrder(lineItems, clientReferenceId);
            }
            else
            {
                if (stripeEvent.Data.Object is not Subscription session) return Ok();


                var item = session.Items.Data.First();
                if (item.Price.Id == _appConfigService.Get<string>(Settings.StripePriceIdForLicense))
                {
                    var license = await _context.Licenses.FirstOrDefaultAsync(x => x.ExternalId == session.Id);


                    if (stripeEvent.Type == Events.CustomerSubscriptionCreated || stripeEvent.Type == Events.CustomerSubscriptionUpdated)
                    {
                        if (license != null)
                            return BadRequest("CustomerSubscriptionCreated but found existing license: " +
                                              license.ExternalId);
                        if (session.Status != "active") return Ok("Subscription is not active.");
                        //resolve Customer
                        var subscriptionOptions = new SubscriptionGetOptions();
                        subscriptionOptions.AddExpand("customer");
                        var subscriptionService = new SubscriptionService();
                        var subscription = await subscriptionService.GetAsync(session.Id, subscriptionOptions);
                        var customerId = subscription.Customer.Id;
                        if (customerId == null)
                            return BadRequest("Customer id is null. This is probably because the user is not logged in.");
                        var userReference =
                            await _context.Users.FirstOrDefaultAsync(x => x.StripeCustomerId == customerId);
                        if (userReference == null)
                            return BadRequest("User not found for customer id: " + session.Customer.Id);
                        await _context.Licenses.AddAsync(new License
                        {
                            User = userReference,
                            Code = Guid.NewGuid().ToString(),
                            ExternalId = session.Id
                        });
                    }
                    else if (stripeEvent.Type == Events.CustomerSubscriptionDeleted)
                    {
                        if (license != null) _context.Licenses.Remove(license);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        catch (StripeException e)
        {
            return BadRequest(e.Message);
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
                if (item.Price.Id == _appConfigService.Get<string>(Settings.StripePriceIdForToken))
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

    [HttpGet]
    public IActionResult License()
    {
        //process to Index
        ViewBag.License = true;
        return View("Index");
    }
}