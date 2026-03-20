using System;

namespace Bennie;

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

    /// <summary>
    /// MSAL authority tenant segment.
    /// "common" supports both work/school (Entra ID) and personal Microsoft accounts.
    /// </summary>
    public static string AuthorityTenant = "common";

    /// <summary>
    /// Scopes for the Datasync API.
    /// </summary>
    public static string[] ApiScopes = ["api://729ead08-e2b0-46ad-98e9-345c5dd2ca3b/access_as_user"];

    /// <summary>
    /// Scopes for Microsoft Graph.
    /// </summary>
    public static string[] GraphScopes = ["User.Read"];

}
