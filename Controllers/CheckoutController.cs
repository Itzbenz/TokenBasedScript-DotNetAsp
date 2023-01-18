using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;

namespace TokenBasedScript.Controllers;

public class CheckoutController : Controller
{

    private readonly MvcContext _context;
     private readonly IGiveUser _giveUser;
    private readonly string STRIPE_WEBHOOK_SECRET = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")!;

    public CheckoutController(MvcContext context, IGiveUser giveUser)
    {
        _context = context;
        _giveUser = giveUser;
    }
   
    public async Task<IActionResult> IndexAsync()
    {
        User? user = await _giveUser.GetUser();
        if(user == null) return RedirectToAction("Index", "Home");
        return View("Index");
    }

    [HttpPost("/{controller}")]
    public async Task<IActionResult> Checkout([FromForm] int amount)
    {
        User? user = await _giveUser.GetUser();
        if(user == null) return RedirectToAction("Index", "Home");
        var client_reference_id = user.Snowflake;
        var domain = Request.Scheme + "://" + Request.Host.Value + "/";
        var options = new SessionCreateOptions
        {
            
            ClientReferenceId = client_reference_id,
            LineItems = new List<SessionLineItemOptions>
                {
                  new SessionLineItemOptions
                  {
                    // Provide the exact Price ID (for example, pr_1234) of the product you want to sell
                    Price = Environment.GetEnvironmentVariable("STRIPE_PRICE_ID")!,
                    Quantity = amount,
                  },
                },
            Mode = "payment",
            SuccessUrl = domain,
            CancelUrl = domain,
        };
        var service = new SessionService();
        Session session = service.Create(options);

        Response.Headers.Add("Location", session.Url);
        return new StatusCodeResult(303);
    }
    [HttpPost("/api/stripe/webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                STRIPE_WEBHOOK_SECRET
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
                var client_reference_id = session.ClientReferenceId;
                if(client_reference_id == null)
                    return BadRequest("Client reference id is null. This is probably because the user is not logged in.");
                // Fulfill the purchase...
                this.FulfillOrder(lineItems, client_reference_id);
            }
            return Ok();
        }
        catch (StripeException e)
        {
            return BadRequest();
        }
    }

    private void FulfillOrder(StripeList<LineItem> lineItems, string client_reference_id)
    {
        //find by snowflake
        User? user = _context.Users.FirstOrDefault(u => u.Snowflake == client_reference_id);
        if (user == null || user.Snowflake == null)
            throw new Exception("User not found: " + client_reference_id);
        foreach (var item in lineItems)
        {
            if (item.Description == "Token")
            {
                user.TokenLeft += item.Quantity??0;
                _context.SaveChanges();
            }
        }
    }
}