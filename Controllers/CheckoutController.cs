using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using TokenBasedScript.Data;
using TokenBasedScript.Models;

namespace TokenBasedScript.Controllers;

public class CheckoutController : Controller
{
    
    private readonly MvcContext _context;

    public CheckoutController(MvcContext context)
    {
        _context = context;
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
                Environment.GetEnvironmentVariable("STRIPE_API_SECRET")
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

                // Fulfill the purchase...
                this.FulfillOrder(lineItems);
            }
            return Ok();
        }
        catch (StripeException e)
        {
            return BadRequest();
        }
    }

    private void FulfillOrder(StripeList<LineItem> lineItems)
    {
        foreach (var item in lineItems)
        {
            if (item.Product.Name == "Token")
            {
                //find the order
                var order = _context.Orders.FirstOrDefault(o => o.Id == item.Id);
                
            }
        }
    }
}