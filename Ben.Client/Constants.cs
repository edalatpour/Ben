using System;

namespace Ben;

public class Constants
{

/// <summary>
    /// The base URI for the Datasync service.
    /// </summary>
    // public static string ServiceUri = "https://app-qg762nqxq5bva.azurewebsites.net/";
    public static string ServiceUri = "https://ca-qg762nqxq5bva.braveplant-d8d95ba6.centralus.azurecontainerapps.io";

    /// <summary>
    /// The application (client) ID for the native app within Microsoft Entra ID
    /// </summary>
    public static string ApplicationId = "d5a4dd1f-e90b-4c48-8031-15041bd3c02c";

    public static string TenantId = "fdcad902-e900-470c-ad85-07deee47cb52";
    /// <summary>
    /// The list of scopes to request
    /// </summary>
    public static string[] Scopes = ["api://729ead08-e2b0-46ad-98e9-345c5dd2ca3b/access_as_user"];
    // 
    
}
