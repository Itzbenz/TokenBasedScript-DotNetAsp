using Microsoft.AspNetCore.Mvc;

namespace TokenBasedScript.Models;

public class NikeOrder
{
    public bool failed;

    /**
    id: number; //External ID

    orderNumber: string; //Nike
    orderEmail: string; // Nike
    myBRTNumber?: string;
    shipmentReference?: string;
    myBRTCode?: string;
    status?: string;
    failed?: boolean;
    postCode?: string;

    vasBrtForm?: VasBrtForm;
    */
    public string id;

    public string? myBRTCode;
    public string? myBRTNumber;
    public string? orderEmail;

    public string? orderNumber;
    public string? postCode;

    public float progressFloat;
    public string? shipmentReference;
    public string? status;
    public VasBrtForm? vasBrtForm;


    public class Form
    {
        public long? id { get; set; }
        public string orderEmail { get; set; }
        public string orderNumber { get; set; }
        public string name { get; set; }
        public string telephone { get; set; }
        public string email { get; set; }
        public string mobilePhoneNumber { get; set; }
        public string address { get; set; }
        public string postCode { get; set; }
        public string town { get; set; }
        public string district { get; set; }
        
  
        public NikeOrder ToNikeOrder()
        {
            return new NikeOrder
            {
                id = id.ToString(),
                orderEmail = orderEmail,
                orderNumber = orderNumber,
                vasBrtForm = new VasBrtForm
                {
                    name = name,
                    telephone = telephone,
                    email = email,
                    mobilePhoneNumber = mobilePhoneNumber,
                    optionsData = new VasBrtForm.OptionsData
                    {
                        name = name,
                        address = address,
                        postCode = postCode,
                        town = town,
                        district = district
                    }
                }
            };
        }
    }

    public class VasBrtForm
    {
        public string name { get; set; }
        public string telephone { get; set; }
        public string email { get; set; }
        public string mobilePhoneNumber { get; set; }
        public OptionsData optionsData { get; set; }

        public class OptionsData
        {
            public string name { get; set; }
            public string address { get; set; }
            public string postCode { get; set; }
            public string town { get; set; }
            public string district { get; set; }
        }
    }
}