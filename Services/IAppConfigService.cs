using System.Reflection;
using TokenBasedScript.Data;
using TokenBasedScript.Models;

namespace TokenBasedScript.Services;



public enum Settings
{
/**
 *   "Script:NikeBRT:Backend:URL": "http://localhost:3000",
  "AllowedHosts": "*",
  "FreeTokenForNewUser": "0",
  "Stripe:Price:ID": "",
  "Stripe:Webhook:Secret": "",
  "Stripe:API:Secret": "",
  "Discord:Client:ID": "",
  "Discord:Client:Secret": "",
  "Discord:User:Admin:ID": "",
 */
    [Setting(Name = "Script Nike BRT Backend URL", Type = typeof(string), AppSettingsKey = "Script:NikeBRT:Backend:URL", DefaultValue = "http://localhost:3000")]
    ScriptNikeBrtBackendUrl,
    [Setting(Name = "Free Token For New User", Type = typeof(int), AppSettingsKey = "FreeTokenForNewUser", DefaultValue = 0)]
    FreeTokenForNewUser,
    [Setting(Name = "Stripe Price ID For Token", Type = typeof(string),  AppSettingsKey = "Stripe:Price:ID")]
    StripePriceIdForToken,
    [Setting(Name = "Stripe Price ID For License", Type = typeof(string))]
    StripePriceIdForLicense,
    [Setting(Name = "Stripe Webhook Secret", Type = typeof(string), AppSettingsKey = "Stripe:Webhook:Secret")]
    StripeWebhookSecret,
    [Setting(Name = "Stripe API Secret", Type = typeof(string), AppSettingsKey = "Stripe:API:Secret")]
    StripeApiSecret,


}

public class Setting : Attribute
{
    public string Name { get; set; } = null!;
    public string? AppSettingsKey { get; set; }
    public Type Type { get; set; } = typeof(string);
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
    private readonly IConfiguration _configuration;
    
    private static bool _isSynced;
    public AppConfigService(MvcContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private T? GetDefaultValue<T>(Setting attr)
    {
        if (attr.DefaultValue == null && attr.AppSettingsKey == null) return default;
        var appSettingsValue = _configuration[attr.AppSettingsKey!];
        if (appSettingsValue == null) return (T?) Convert.ChangeType(attr.DefaultValue, typeof(T));
        return (T?) Convert.ChangeType(appSettingsValue, typeof(T));
    }
    private void Sync()
    {
        if (_isSynced) return;
        _isSynced = true;
        //declare settings
        var settings = new List<Models.Settings>();
        foreach (var setting in Enum.GetValues<Settings>())
        {
            var attribute = setting.GetType().GetField(setting.ToString())!.GetCustomAttribute<Setting>();
            if (attribute == null) throw new Exception("Setting not found for " + setting);
            
            if(attribute.DefaultValue != null && attribute.Type != attribute.DefaultValue.GetType())
                throw new Exception("Setting default value type mismatch for " + setting + " expect " + attribute.Type.Name + " but got " + attribute.DefaultValue.GetType().Name);
           
            settings.Add(new Models.Settings
            {
                Name = attribute.Name,
                Type = attribute.Type.Name,
                DefaultValueString = GetDefaultValue<string>(attribute) ?? "",
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
            }
            dbSetting.DefaultValueString = setting.DefaultValueString;//read only
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
        Sync();
        var attr = settings.GetType().GetField(settings.ToString())!.GetCustomAttribute<Setting>();
        if (attr == null) throw new Exception("Setting not found for " + settings);
        var key = attr.Name;
        //only support primitive types
        if(!typeof(T).IsPrimitive && typeof(T) != typeof(string))
            throw new Exception("Only support primitive types got " + typeof(T).Name + " for " + key);
        var setting = _context.Settings.FirstOrDefault(s => s.Name == key);
        if (setting == null)
        {
            throw new Exception("Setting not found: " + key);
        }

        //assert type
        if (setting.Type != typeof(T).Name)
        {
            throw new Exception("Setting type mismatch: " + key + " expect " + setting.Type + " but got " + typeof(T).Name);
        }

        if (setting.ValueString != null) return (T) Convert.ChangeType(setting.ValueString, typeof(T));
        //check DefaultValue
        var attrDefaultValue = GetDefaultValue<string>(attr);
        if (attrDefaultValue == null)
        {
            //use default value
            return defaultValue;
        }

        //use DefaultValue
        return (T) Convert.ChangeType(attrDefaultValue, typeof(T));

    }
    
    public void Set<T>(Settings settings, T value)
    {
        Sync();
        var attr = settings.GetType().GetField(settings.ToString())!.GetCustomAttribute<Setting>();
        if (attr == null) throw new Exception("Setting not found for " + settings);
        var key = attr.Name;
        if(!typeof(T).IsPrimitive && typeof(T) != typeof(string)) 
            throw new Exception("Only support primitive types got " + typeof(T).Name + " for " + key);
        var setting = _context.Settings.FirstOrDefault(s => s.Name == key);
        if (setting == null)
        {
            throw new Exception("Setting not found: " + key);
        }

        //assert type
        if (setting.Type != typeof(T).Name)
        {
            throw new Exception("Setting type mismatch: " + key + " expect " + setting.Type + " but got " + typeof(T).Name);
        }
        setting.ValueString = value?.ToString();
        _context.SaveChanges();
    }
    
}