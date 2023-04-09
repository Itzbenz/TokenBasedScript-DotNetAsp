using System.Reflection;
using TokenBasedScript.Data;
using TokenBasedScript.Models;

namespace TokenBasedScript.Services;



public enum Settings
{

    [Setting(Name = "Script Nike BRT Backend URL", Type = typeof(string), DefaultValue = "http://localhost:3000")]
    ScriptNikeBrtBackendUrl,
    [Setting(Name = "Free Token For New User", Type = typeof(int), DefaultValue = 0)]
    FreeTokenForNewUser,
    [Setting(Name = "Stripe Price ID For NikeBRT", Type = typeof(string))]
    StripePriceIdForNikeBrt,
    [Setting(Name = "Stripe Price ID For License", Type = typeof(string))]
    StripePriceIdForLicense,
    [Setting(Name = "Stripe Webhook Secret", Type = typeof(string))]
    StripeWebhookSecret,
    [Setting(Name = "Stripe API Secret", Type = typeof(string))]
    StripeApiSecret,


}

public class Setting : Attribute
{
    public string Name { get; set; } = null!;
    public Type Type { get; set; } = null!;
    public object? DefaultValue { get; set; }
}

public interface IAppConfigService
{
    public T? Get<T>(Settings key, T? defaultValue = default);
    public void Set<T>(Settings key, T value);
}

public class AppConfigService : IAppConfigService
{
    private readonly MvcContext _context;
    private static bool _isSynced;
    public AppConfigService(MvcContext context)
    {
        _context = context;
        if (_isSynced) return;
        Sync();
        _isSynced = true;
    }

    private void Sync()
    {
        //declare settings
        var settings = new List<Models.Settings>();
        foreach (var setting in Enum.GetValues<Settings>())
        {
            var attribute = setting.GetType().GetCustomAttribute<Setting>();
            
            if (attribute == null) throw new Exception("Setting not found for " + setting);
            settings.Add(new Models.Settings
            {
                Name = attribute.Name,
                Type = attribute.Type.Name,
                DefaultValueString = attribute.DefaultValue?.ToString() ?? string.Empty,
            });
        }
        
        //synchronize database
        var dbSettings = _context.Settings.ToList();
        foreach (var dbSetting in dbSettings)
        {
            var setting = settings.FirstOrDefault(s => s.Name == dbSetting.Name);
            //remove extra settings    
            if (setting == null)
            {
                _context.Settings.Remove(dbSetting);
                continue;
            }
            
            //check if settings get different type
            if (setting.Type != dbSetting.Type)
            {
                //update
                dbSetting.Type = setting.Type;
                dbSetting.ValueString = setting.DefaultValueString;
                dbSetting.DefaultValueString = setting.DefaultValueString;
            }
            
            //check if settings get different default value
            if (setting.DefaultValueString != dbSetting.DefaultValueString)
            {
                //update
                dbSetting.DefaultValueString = setting.DefaultValueString;
            }
        }
        
        //add new settings
        foreach (var setting in settings.Where(setting => dbSettings.FirstOrDefault(s => s.Name == setting.Name) == null))
        {
            _context.Settings.Add(setting);
        }
        
        _context.SaveChanges();
    }
    
    public T? Get<T>(Settings settings, T? defaultValue=default)
    {
        var attr = settings.GetType().GetCustomAttribute<Setting>();
        if (attr == null) throw new Exception("Setting not found for " + settings);
        var key = attr.Name;
        //only support primitive types
        if(!typeof(T).IsPrimitive) throw new Exception("Only support primitive types");
        var setting = _context.Settings.FirstOrDefault(s => s.Name == key);
        if (setting == null)
        {
            throw new Exception("Setting not found: " + key);
        } else {
            //assert type
            if (setting.Type != typeof(T).Name)
            {
                throw new Exception("Setting type mismatch: " + key + " expect " + setting.Type + " but got " + typeof(T).Name);
            }
        }

        if (setting.ValueString != null) return (T) Convert.ChangeType(setting.ValueString, typeof(T));
        //check DefaultValue
        if (attr.DefaultValue == null)
        {
            //use default value
            return defaultValue;
        }
        else
        {
            //use DefaultValue
            return (T) Convert.ChangeType(attr.DefaultValue, typeof(T));
        }

    }
    
    public void Set<T>(Settings settings, T value)
    {
        var attr = settings.GetType().GetCustomAttribute<Setting>();
        if (attr == null) throw new Exception("Setting not found for " + settings);
        var key = attr.Name;
        if(!typeof(T).IsPrimitive) throw new Exception("Only support primitive types");
        var setting = _context.Settings.FirstOrDefault(s => s.Name == key);
        if (setting == null)
        {
            throw new Exception("Setting not found: " + key);
        }
        else
        {
            //assert type
            if (setting.Type != typeof(T).Name)
            {
                throw new Exception("Setting type mismatch: " + key + " expect " + setting.Type + " but got " + typeof(T).Name);
            }
            setting.ValueString = value?.ToString();
        }
        _context.SaveChanges();
    }
    
}