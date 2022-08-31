# Moesif Middleware for .NET

[![Built For][ico-built-for]][link-built-for]
[![Latest Version][ico-version]][link-package]
[![Software License][ico-license]][link-license]
[![Source Code][ico-source]][link-source]

Middleware SDK that captures _incoming_ or _outgoing_ API calls from .NET apps and sends to Moesif API Analytics.

[Source Code on GitHub](https://github.com/Moesif/moesif-dotnet)

## How to install

Install the Nuget Package:

```bash
Install-Package Moesif.Middleware
```

Jump to installation for your specific framework:

- [.Net Core and .NET 5 installation](#net-core-installation)
- [.NET Framework installation](#net-framework-installation)

## Net Core installation

> The below installation is intended for .NET 5 or .NET Core 2.1 and higher. For .NET Framework, go to [.NET Framework installation](#net-framework-installation).

In `Startup.cs` file in your project directory, please add `app.UseMiddleware<MoesifMiddleware>(moesifOptions);` to the pipeline.

To collect the most context, it is recommended to add the middleware after other middleware such as SessionMiddleware and AuthenticationMiddleware. 

Add the middleware to your application:

```csharp
using Moesif.Middleware;

public class Startup {

    // moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
    Dictionary<string, object> moesifOptions = new Dictionary<string, object>
    {
        {"ApplicationId", "Your Moesif Application Id"},
        {"LogBody", true},
        ...
        # For other options see below.
    };

    
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc();
    }

    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {

        app.UseMiddleware<MoesifMiddleware>(moesifOptions);
        app.UseMvc();
    }
}
```

Your Moesif Application Id can be found in the [_Moesif Portal_](https://www.moesif.com/).
After signing up for a Moesif account, your Moesif Application Id will be displayed during the onboarding steps. 

You can always find your Moesif Application Id at any time by logging 
into the [_Moesif Portal_](https://www.moesif.com/), click on the top-right menu,
 and then clicking _Installation_.
 
### .NET Core example

Checkout the [examples](https://github.com/Moesif/moesif-netcore-example)
using .NET Core 2.0 and .NET Core 3.0

### .NET Core options

#### __`ApplicationId`__
(__required__), _string_, is obtained via your Moesif Account, this is required.

#### __`Skip`__
(optional) _(HttpRequest, HttpResponse) => boolean_, a function that takes a request and a response, and returns true if you want to skip this particular event.

#### __`IdentifyUser`__
(optional) _(HttpRequest, HttpResponse) => string_, a function that takes a request and a response, and returns a string that is the user id used by your system. While Moesif identify users automatically, if your set up is very different from the standard implementations, it would be helpful to provide this function.

#### __`IdentifyCompany`__
(optional) _(HttpRequest, HttpResponse) => string_, a function that takes a request and a response, and returns a string that is the company id for this event.

#### __`GetSessionToken`__
(optional) _(HttpRequest, HttpResponse) => string_, a function that takes a request and a response, and returns a string that is the session token for this event. Again, Moesif tries to get the session token automatically, but if you setup is very different from standard, this function will be very help for tying events together, and help you replay the events.

#### __`GetMetadata`__
(optional) _(HttpRequest, HttpResponse) => dictionary_, getMetadata is a function that returns an object that allows you
to add custom metadata that will be associated with the event. The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a VM instance_id, a trace_id, or a tenant_id with the request.

#### __`MaskEventModel`__
(optional) _(EventModel) => EventModel_, a function that takes an EventModel and returns an EventModel with desired data removed. Use this if you prefer to write your own mask function. The return value must be a valid EventModel required by Moesif data ingestion API. For details regarding EventModel please see the [Moesif CSharp API Documentation](https://www.moesif.com/docs/api?csharp#).

```csharp
public static Func<EventModel, EventModel> MaskEventModel = (EventModel moesifEvent) =>
{
    Dictionary<String, String> eventRequestHeaders = moesifEvent.Request.Headers;
    bool keyExists = eventRequestHeaders.ContainsKey("Authorization");
    if (keyExists)
    {
        eventRequestHeaders.Remove("Authorization");
    };

    return moesifEvent;
};
```

#### __`ApiVersion`__
(optional), _string_, API version associated with this particular event.

#### __`LocalDebug`__
_boolean_, set to true to print internal log messages for debugging SDK integration issues.

#### __`LogBody`__
_boolean_, default true. Set to false to not log the request and response body to Moesif.

#### __`AuthorizationHeaderName`__
(optional), _string_, Request header containing a Bearer or Basic token to extract user id. Also, supports a comma-separated string. We will check headers in order like "X-Api-Key,Authorization".

#### __`AuthorizationUserIdField`__
(optional), _string_, Field name in JWT/OpenId token's payload for identifying users. Only applicable if authorization_header_name is set and is a Bearer token.

#### __`Capture_Outgoing_Requests`__
(optional), Set to capture all outgoing API calls from your app to third parties like Stripe or to your own dependencies while using [System.Net.Http](https://docs.microsoft.com/en-us/dotnet/api/system.net.http?view=netframework-4.8) package. The options below is applied to outgoing API calls. When the request is outgoing, for options functions that take request and response as input arguments, the request and response objects passed in are [HttpRequestMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httprequestmessage) request and [HttpResponseMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httpresponsemessage) response objects.

How to configure your application to start capturing outgoing API calls.

```csharp
using System.Net.Http;
using Moesif.Middleware.Helpers;

// moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
MoesifCaptureOutgoingRequestHandler handler = new MoesifCaptureOutgoingRequestHandler(new HttpClientHandler(), moesifOptions);
HttpClient client = new HttpClient(handler);
```

#### __`GetMetadataOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => dictionary_, getMetadata is a function that returns an object that allows you
to add custom metadata that will be associated with the event. The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a VM instance_id, a trace_id, or a tenant_id with the request.

#### __`GetSessionTokenOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the session token for this event. Again, Moesif tries to get the session token automatically, but if you setup is very different from standard, this function will be very help for tying events together, and help you replay the events.

#### __`IdentifyUserOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the user id used by your system. While Moesif identify users automatically, if your set up is very different from the standard implementations, it would be helpful to provide this function.

#### __`IdentifyCompanyOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the company id for this event.

#### __`SkipOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => boolean_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns true if you want to skip this particular event.

#### __`MaskEventModelOutgoing`__
(optional) _(EventModel) => EventModel_, a function that takes an EventModel and returns an EventModel with desired data removed. Use this if you prefer to write your own mask function. The return value must be a valid EventModel required by Moesif data ingestion API. For details regarding EventModel please see the [Moesif CSharp API Documentation](https://www.moesif.com/docs/api?csharp#).

#### __`LogBodyOutgoing`__
_boolean_, default true. Set to false to not log the request and response body to Moesif.

### Example configuration:

```csharp
public static Func<HttpRequest, HttpResponse, string> IdentifyUser = (HttpRequest req, HttpResponse res) => {
    // Implement your custom logic to return user id
    return req.HttpContext?.User?.Identity?.Name;
};

public static Func<HttpRequest, HttpResponse, string> IdentifyCompany = (HttpRequest req, HttpResponse res) => {
    return req.Headers["X-Organization-Id"];
};

public static Func<HttpRequest, HttpResponse, string> GetSessionToken = (HttpRequest req, HttpResponse res) => {
    return req.Headers["Authorization"];
};

public static Func<HttpRequest, HttpResponse, Dictionary<string, object>> GetMetadata = (HttpRequest req, HttpResponse res) => {
    Dictionary<string, object> metadata = new Dictionary<string, object>
    {
        {"string_field", "value_1"},
        {"number_field", 0},
        {"object_field", new Dictionary<string, string> {
            {"field_a", "value_a"},
            {"field_b", "value_b"}
            }
        }
    };
    return metadata;
};

static public Dictionary<string, object> moesifOptions = new Dictionary<string, object>
{
    {"ApplicationId", "Your Moesif Application Id"},
    {"ApiVersion", "1.1.0"},
    {"IdentifyUser", IdentifyUser},
    {"IdentifyCompany", IdentifyCompany},
    {"GetSessionToken", GetSessionToken},
    {"GetMetadata", GetMetadata}
};
```

## NET Framework installation

> The below installation is intended for .NET Framework 4.5 and higher. For .NET 5 or .NET 2.1 and higher, go to [.Net Core / .NET 5 installation](#net-core-installation).

In `Startup.cs` file in your project directory, please add `app.Use<MoesifMiddleware>(moesifOptions);` to the pipeline.

To collect the most context, it is recommended to add the middleware after other middleware such as SessionMiddleware and AuthenticationMiddleware. 

> If your app uses Windows Communication Foundation (WCF), set [DisableStreamOverride](#disablestreamoverride) to true

Add the middleware to your application:

```csharp
using Moesif.Middleware;

public class Startup {
    
    // moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
    Dictionary<string, object> moesifOptions = new Dictionary<string, object>
    {
        {"ApplicationId", "Your Moesif Application Id"},
        {"LogBody", true},
        ...
        # For other options see below.
    };

    public void Configuration(IAppBuilder app)
    {
        app.Use<MoesifMiddleware>(moesifOptions);
    }
}
```

Your Moesif Application Id can be found in the [_Moesif Portal_](https://www.moesif.com/).
After signing up for a Moesif account, your Moesif Application Id will be displayed during the onboarding steps. 

You can always find your Moesif Application Id at any time by logging 
into the [_Moesif Portal_](https://www.moesif.com/), click on the top-right menu,
 and then clicking _Installation_.

### Add OWIN dependencies

#### IIS integrated pipeline
If you're running your .NET app on IIS (or Visual Studio IIS Express) using integrated mode (most common), you will have to install the OWIN SystemWeb package if not done so already:
Review [OWIN Middleware in the IIS integrated pipeline](https://docs.microsoft.com/en-us/aspnet/aspnet/overview/owin-and-katana/owin-middleware-in-the-iis-integrated-pipeline) for more info. 

```bash
Install-Package Microsoft.Owin.Host.SystemWeb
```

Moesif does not support IIS running in Classic mode (Backwards compatibility for IIS 6.0). Unless your app predates IIS 6.0 and requires classic mode, you should switch to integrated mode.  
{: .notice--primary}

#### Self-Host executable
While uncommon, if your application is a self-hosted executable that does not run on IIS, you may have to install the SelfHost package if not done so already:

[For .NET Web API applications](https://docs.microsoft.com/en-us/aspnet/web-api/overview/hosting-aspnet-web-api/use-owin-to-self-host-web-api):

```bash
Install-Package Microsoft.AspNet.WebApi.OwinSelfHost
```

[For all other .NET applications](https://docs.microsoft.com/en-us/aspnet/aspnet/overview/owin-and-katana/getting-started-with-owin-and-katana#self-host-owin-in-a-console-application):

```bash
Install-Package Microsoft.Owin.SelfHost -Pre
```

### .NET Framework Examples
The following examples are available for .NET Framework with Moesif:

- [.NET Framework Example](https://github.com/Moesif/moesif-netframework-example) using .NET Framework 4.6.1 and IIS
- [.NET Framework SelfHost Example](https://github.com/Moesif/moesif-aspnet-webapi-selfhost-example) using .NET Framework 4.6.1 _(SelfHost is uncommon)_

### .NET Framework options

_The request and response objects passed in are [IOwinRequest](https://docs.microsoft.com/en-us/previous-versions/aspnet/dn308194(v%3Dvs.113)) request and [IOwinResponse](https://docs.microsoft.com/en-us/previous-versions/aspnet/dn308204(v%3Dvs.113)) response objects._

#### __`ApplicationId`__
(__required__), _string_, is obtained via your Moesif Account, this is required.

#### __`Skip`__
(optional) _(IOwinRequest, IOwinResponse) => boolean_, a function that takes a request and a response, and returns true if you want to skip this particular event.

#### __`IdentifyUser`__
(optional) _(IOwinRequest, IOwinResponse) => string_, a function that takes a request and a response, and returns a string that is the user id used by your system. While Moesif identify users automatically, if your set up is very different from the standard implementations, it would be helpful to provide this function.

#### __`IdentifyCompany`__
(optional) _(IOwinRequest, IOwinResponse) => string_, a function that takes a request and a response, and returns a string that is the company id for this event.

#### __`GetSessionToken`__
(optional) _(IOwinRequest, IOwinResponse) => string_, a function that takes a request and a response, and returns a string that is the session token for this event. Again, Moesif tries to get the session token automatically, but if you setup is very different from standard, this function will be very help for tying events together, and help you replay the events.

#### __`GetMetadata`__
(optional) _(IOwinRequest, IOwinResponse) => dictionary_, getMetadata is a function that returns an object that allows you
to add custom metadata that will be associated with the event. The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a VM instance_id, a trace_id, or a tenant_id with the request.

#### __`MaskEventModel`__
(optional) _(EventModel) => EventModel_, a function that takes an EventModel and returns an EventModel with desired data removed. Use this if you prefer to write your own mask function. The return value must be a valid EventModel required by Moesif data ingestion API. For details regarding EventModel please see the [Moesif CSharp API Documentation](https://www.moesif.com/docs/api?csharp#).

```csharp
public static Func<EventModel, EventModel> MaskEventModel = (EventModel moesifEvent) =>
{
    Dictionary<String, String> eventRequestHeaders = moesifEvent.Request.Headers;
    bool keyExists = eventRequestHeaders.ContainsKey("Authorization");
    if (keyExists)
    {
        eventRequestHeaders.Remove("Authorization");
    };

    return moesifEvent;
};
```

#### __`ApiVersion`__
(optional), _string_, api version associated with this particular event.

#### __`LocalDebug`__
_boolean_, set to true to print internal log messages for debugging SDK integration issues.

#### __`LogBody`__
_boolean_, default true. Set to false to not log the request and response body to Moesif.

#### __`DisableStreamOverride`__
_boolean_, Set to true to disable overriding the request body stream. This is required if your app is using Windows Communication Foundation (WCF). Otherwise, you may experience issues when your business logic accesses the request body. 

#### __`AuthorizationHeaderName`__
(optional), _string_, Request header containing a Bearer or Basic token to extract user id. Also, supports a comma separated string. We will check headers in order like "X-Api-Key,Authorization".

#### __`AuthorizationUserIdField`__
(optional), _string_, Field name in JWT/OpenId token's payload for identifying users. Only applicable if authorization_header_name is set and is a Bearer token.

#### __`Capture_Outgoing_Requests`__
(optional), Set to capture all outgoing API calls from your app to third parties like Stripe or to your own dependencies while using [System.Net.Http](https://docs.microsoft.com/en-us/dotnet/api/system.net.http?view=netframework-4.8) package. The options below is applied to outgoing API calls. When the request is outgoing, for options functions that take request and response as input arguments, the request and response objects passed in are [HttpRequestMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httprequestmessage) request and [HttpResponseMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httpresponsemessage) response objects.

How to configure your application to start capturing outgoing API calls.

```csharp
using System.Net.Http;
using Moesif.Middleware.Helpers;

// moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
MoesifCaptureOutgoingRequestHandler handler = new MoesifCaptureOutgoingRequestHandler(new HttpClientHandler(), moesifOptions);
HttpClient client = new HttpClient(handler);
```

#### __`GetMetadataOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => dictionary_, getMetadata is a function that returns an object that allows you
to add custom metadata that will be associated with the event. The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a VM instance_id, a trace_id, or a tenant_id with the request.

#### __`GetSessionTokenOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the session token for this event. Again, Moesif tries to get the session token automatically, but if you setup is very different from standard, this function will be very help for tying events together, and help you replay the events.

#### __`IdentifyUserOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the user id used by your system. While Moesif identify users automatically, if your set up is very different from the standard implementations, it would be helpful to provide this function.

#### __`IdentifyCompanyOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the company id for this event.

#### __`SkipOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => boolean_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns true if you want to skip this particular event.

#### __`MaskEventModelOutgoing`__
(optional) _(EventModel) => EventModel_, a function that takes an EventModel and returns an EventModel with desired data removed. Use this if you prefer to write your own mask function. The return value must be a valid EventModel required by Moesif data ingestion API. For details regarding EventModel please see the [Moesif CSharp API Documentation](https://www.moesif.com/docs/api?csharp#).

#### __`LogBodyOutgoing`__
_boolean_, default true. Set to false to not log the request and response body to Moesif.

### Example Configuration:

```csharp
public static Func<IOwinRequest, IOwinResponse, string> IdentifyUser = (IOwinRequest req, IOwinResponse res) => {
    // Implement your custom logic to return user id
    return req?.Context?.Authentication?.User?.Identity?.Name;
};

public static Func<IOwinRequest, IOwinResponse, string> IdentifyCompany = (IOwinRequest req, IOwinResponse res) => {
    return req.Headers["X-Organization-Id"];
};

public static Func<IOwinRequest, IOwinResponse, string> GetSessionToken = (IOwinRequest req, IOwinResponse res) => {
    return req.Headers["Authorization"];
};

public static Func<IOwinRequest, IOwinResponse, Dictionary<string, object>> GetMetadata = (IOwinRequest req, IOwinResponse res) => {
    Dictionary<string, object> metadata = new Dictionary<string, object>
    {
        {"string_field", "value_1"},
        {"number_field", 0},
        {"object_field", new Dictionary<string, string> {
            {"field_a", "value_a"},
            {"field_b", "value_b"}
            }
        }
    };
    return metadata;
};

static public Dictionary<string, object> moesifOptions = new Dictionary<string, object>
{
    {"ApplicationId", "Your Moesif Application Id"},
    {"LocalDebug", true},
    {"LogBody", true},
    {"LogBodyOutgoing", true},
    {"ApiVersion", "1.1.0"},
    {"IdentifyUser", IdentifyUser},
    {"IdentifyCompany", IdentifyCompany},
    {"GetSessionToken", GetSessionToken},
    {"GetMetadata", GetMetadata},
    {"BatchSize", 25}
};
```
## Update a Single User

Create or update a user profile in Moesif.
The metadata field can be any customer demographic or other info you want to store.
Only the `user_id` field is required.
For details, visit the [C# API Reference](https://www.moesif.com/docs/api?csharp#update-a-user).

```csharp
// Campaign object is optional, but useful if you want to track ROI of acquisition channels
// See https://www.moesif.com/docs/api#users for campaign schema
Dictionary<string, object> campaign = new Dictionary<string, object>
{
    {"utm_source", "google"},
    {"utm_medium", "cpc"},
    {"utm_campaign", "adwords"},
    {"utm_term", "api+tooling"},
    {"utm_content", "landing"}
};

// metadata can be any custom dictionary
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "john@acmeinc.com"},
    {"first_name", "John"},
    {"last_name", "Doe"},
    {"title", "Software Engineer"},
    {"sales_info", new Dictionary<string, object> {
            {"stage", "Customer"},
            {"lifetime_value", 24000},
            {"account_owner", "mary@contoso.com"}
        }
    }
};

// Only user_id is required
Dictionary<string, object> user = new Dictionary<string, object>
{
    {"user_id", "12345"},
    {"company_id", "67890"}, // If set, associate user with a company object
    {"campaign", campaign},
    {"metadata", metadata},
};

// .NET Core
MoesifMiddleware moesifMiddleware = new MoesifMiddleware(Dictionary<string, object> moesifOptions)

// .NET Framework
// MoesifMiddleware moesifMiddleware = new MoesifMiddleware(OwinMiddleware next, Dictionary<string, object> moesifOptions)


// Update the user asynchronously
moesifMiddleware.UpdateUser(user);
```

## Update Users in Batch

Similar to UpdateUser, but used to update a list of users in one batch. 
Only the `user_id` field is required.
This method is a convenient helper that calls the Moesif API lib.
For details, visit the [C# API Reference](https://www.moesif.com/docs/api?csharp#update-users-in-batch).

```csharp
List<Dictionary<string, object>> usersBatch = new List<Dictionary<string, object>>();

Dictionary<string, object> metadataA = new Dictionary<string, object>
{
    {"email", "john@acmeinc.com"},
    {"first_name", "John"},
    {"last_name", "Doe"},
    {"title", "Software Engineer"},
    {"sales_info", new Dictionary<string, object> {
            {"stage", "Customer"},
            {"lifetime_value", 24000},
            {"account_owner", "mary@contoso.com"}
        }
    }
};

// Only user_id is required
Dictionary<string, object> userA = new Dictionary<string, object>
{
    {"user_id", "12345"},
    {"company_id", "67890"}, // If set, associate user with a company object
    {"metadata", metadataA},
};

Dictionary<string, object> metadataB = new Dictionary<string, object>
{
    {"email", "mary@acmeinc.com"},
    {"first_name", "Mary"},
    {"last_name", "Jane"},
    {"title", "Software Engineer"},
    {"sales_info", new Dictionary<string, object> {
            {"stage", "Customer"},
            {"lifetime_value", 24000},
            {"account_owner", "mary@contoso.com"}
        }
    }
};

// Only user_id is required
Dictionary<string, object> userB = new Dictionary<string, object>
{
    {"user_id", "54321"},
    {"company_id", "67890"}, // If set, associate user with a company object
    {"metadata", metadataB},
};

usersBatch.Add(userA);
usersBatch.Add(userB);

// .NET Core
MoesifMiddleware moesifMiddleware = new MoesifMiddleware(Dictionary<string, object> moesifOptions)

// .NET Framework
// MoesifMiddleware moesifMiddleware = new MoesifMiddleware(OwinMiddleware next, Dictionary<string, object> moesifOptions)

moesifMiddleware.UpdateUsersBatch(usersBatch);
```

## Update a Single Company
Create or update a company profile in Moesif.
The metadata field can be any company demographic or other info you want to store.
Only the `company_id` field is required.
This method is a convenient helper that calls the Moesif API lib.
For details, visit the [C# API Reference](https://www.moesif.com/docs/api?csharp#update-a-company).

```csharp
// Campaign object is optional, but useful if you want to track ROI of acquisition channels
// See https://www.moesif.com/docs/api#update-a-company for campaign schema
Dictionary<string, object> campaign = new Dictionary<string, object>
{
    {"utm_source", "google"},
    {"utm_medium", "cpc"},
    {"utm_campaign", "adwords"},
    {"utm_term", "api+tooling"},
    {"utm_content", "landing"}
};

// metadata can be any custom dictionary
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"org_name", "Acme, Inc"},
    {"plan_name", "Free"},
    {"deal_stage", "Lead"},
    {"mrr", 24000},
    {"demographics", new Dictionary<string, object> {
            {"alexa_ranking", 500000},
            {"employee_count", 47}
        }
    }
};

Dictionary<string, object> company = new Dictionary<string, object>
{
    {"company_id", "67890"}, // The only required field is your company id
    {"company_domain", "acmeinc.com"}, // If domain is set, Moesif will enrich your profiles with publicly available info 
    {"campaign", campaign},
    {"metadata", metadata},
};

// .NET Core
MoesifMiddleware moesifMiddleware = new MoesifMiddleware(Dictionary<string, object> moesifOptions)

// .NET Framework
// MoesifMiddleware moesifMiddleware = new MoesifMiddleware(OwinMiddleware next, Dictionary<string, object> moesifOptions)

// Update the company asynchronously
moesifMiddleware.UpdateCompany(company);
```

## Update Companies in Batch

Similar to updateCompany, but used to update a list of companies in one batch. 
Only the `company_id` field is required.
This method is a convenient helper that calls the Moesif API lib.
For details, visit the [C# API Reference](https://www.moesif.com/docs/api?csharp#update-companies-in-batch).


```csharp
List<Dictionary<string, object>> companiesBatch = new List<Dictionary<string, object>>();
// metadata can be any custom dictionary
Dictionary<string, object> metadataA = new Dictionary<string, object>
{
    {"org_name", "Acme, Inc"},
    {"plan_name", "Free"},
    {"deal_stage", "Lead"},
    {"mrr", 24000},
    {"demographics", new Dictionary<string, object> {
            {"alexa_ranking", 500000},
            {"employee_count", 47}
        }
    }
};

Dictionary<string, object> companyA = new Dictionary<string, object>
{
    {"company_id", "67890"}, // The only required field is your company id
    {"company_domain", "acmeinc.com"}, // If domain is set, Moesif will enrich your profiles with publicly available info 
    {"metadata", metadataA},
};

// metadata can be any custom dictionary
Dictionary<string, object> metadataB = new Dictionary<string, object>
{
    {"org_name", "Contoso, Inc"},
    {"plan_name", "Starter"},
    {"deal_stage", "Lead"},
    {"mrr", 48000},
    {"demographics", new Dictionary<string, object> {
            {"alexa_ranking", 500000},
            {"employee_count", 59}
        }
    }
};

Dictionary<string, object> companyB = new Dictionary<string, object>
{
    {"company_id", "09876"}, // The only required field is your company id
    {"company_domain", "contoso.com"}, // If domain is set, Moesif will enrich your profiles with publicly available info 
    {"metadata", metadataB},
};

companiesBatch.Add(companyA);
companiesBatch.Add(companyB);

// .NET Core
MoesifMiddleware moesifMiddleware = new MoesifMiddleware(Dictionary<string, object> moesifOptions)

// .NET Framework
// MoesifMiddleware moesifMiddleware = new MoesifMiddleware(OwinMiddleware next, Dictionary<string, object> moesifOptions)

moesifMiddleware.UpdateCompaniesBatch(companiesBatch);
```

## Troubleshooting

### Issue reading request body in WCF
Certain serializers for Windows Communication Foundation (WCF) may not correctly bind the request body when using logging middleware like Moesif.
If your app uses Windows Communication Foundation (WCF), you may find that your business logic has errors accessing the request body such as for `POST` and `PUT` requests.

To fix, set the option [DisableStreamOverride](#disablestreamoverride) to true like so:

```csharp
Dictionary<string, object> moesifOptions = new Dictionary<string, object>
{
    {"ApplicationId", "Your Moesif Application Id"},
    {"DisableStreamOverride", true},
};
```

### Traditional monolith website broken
Some monolith apps which contain both a website and an API in the same app may issues when API logging middleware is enabled.
This is usually due to interactions with other custom middleware. 

Since usually this custom middleware is enabled for the website only, the recommended fix is to enable Moesif only for your API.
To do, use the `MapWhen` as shown below which only activates the middleware if the Path contains `/api`

```csharp
    app.MapWhen(context => context.Request.Path.ToString().Contains("/api"), appBuilder =>
    {

        appBuilder.Use<MoesifMiddleware>(new System.Collections.Generic.Dictionary<string, object> {
            {"ApplicationId", "Your Moesif Application Id"}
        });
    });
```

### The response body not logged
For .NET Core and .NET 5, you will need to set the following option to ensure the response body is logged:
[More info](https://khalidabuhakmeh.com/dotnet-core-3-dot-0-allowsynchronousio-workaround) on this workaround.

```csharp
    .ConfigureKestrel((context, options) => {
        options.AllowSynchronousIO = true;
    });
```

## Ensuring Errors handled by ExceptionHandler are logged
To capture the API calls handled by ExceptionHandler, please ensure that the `app.UseMiddleware<MoesifMiddleware>(moesifOptions);` is before the `app.UseExceptionHandler()` in the pipeline. 

## How to test

1. Manually clone the git repo
2. From terminal/cmd navigate to the root directory of the middleware.
3. Invoke 'Install-Package Moesif.Middleware'
4. Add your own application id to 'Moesif.Middleware.Test/MoesifMiddlewareTest.cs'. You can find your Application Id from [_Moesif Dashboard_](https://www.moesif.com/) -> _Top Right Menu_ -> _Installation_
5. The tests are contained in the Moesif.Middleware.Test project. In order to invoke these test cases, you will need NUnit 3.0 Test Adapter Extension for Visual Studio. Once the SDK is complied, the test cases should appear in the Test Explorer window. Here, you can click Run All to execute these test cases.

## Tested versions

Moesif has validated `Moesif.Middleware` against the following framework.

|                | Framework Version  |
| -------------- | -----------------  | 
| .NET |5.0|
| .NET |6.0|
| .NET Core|2.0-3.0|
| .NET Framework MVC |4.5-4.7|
| .NET Framework Web API|4.5-4.7|
| .NET Framework Web API SelfHost|4.5-4.7|

## Other integrations

To view more documentation on integration options, please visit __[the Integration Options Documentation](https://www.moesif.com/docs/getting-started/integration-options/).__

[ico-built-for]: https://img.shields.io/badge/built%20for-dotnet-blue.svg
[ico-version]: https://img.shields.io/nuget/v/Moesif.Middleware.svg
[ico-downloads]: https://img.shields.io/nuget/dt/Moesif.Middleware.svg
[ico-license]: https://img.shields.io/badge/License-Apache%202.0-green.svg
[ico-source]: https://img.shields.io/github/last-commit/moesif/moesifdjango.svg?style=social

[link-built-for]: https://www.microsoft.com/net
[link-package]: https://www.nuget.org/packages/Moesif.Middleware
[link-downloads]: https://www.nuget.org/packages/Moesif.Middleware
[link-license]: https://raw.githubusercontent.com/Moesif/moesif-dotnet/master/LICENSE
[link-source]: https://github.com/Moesif/moesif-dotnet
