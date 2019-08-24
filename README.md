# Moesif Middleware for DotNet

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

This SDK supports both .NET Core 2.0 or higher and .NET Framework 4.5 or higher.

- [.Net Core installation](#net-core-installation)
- [.NET Framework installation](#net-framework-installation)

## Net Core installation

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
into the [_Moesif Portal_](https://www.moesif.com/), click on the top right menu,
 and then clicking _Installation_.
 
### .NET Core example

Checkout the [Moesif .Net Core Example](https://github.com/Moesif/moesif-netcore-example)
using .NET Core 2.0

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

#### __`ApiVersion`__
(optional), _string_, api version associated with this particular event.

#### __`LocalDebug`__
_boolean_, set to true to print internal log messages for debugging SDK integration issues.

#### __`LogBody`__
_boolean_, default true. Set to false to not log the request and response body to Moesif.

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
public static Func<HttpRequest, HttpResponse, string> IdentifyUser = (HttpRequest req, HttpResponse res) =>  {
    return "my_user_id";  
} ;

public static Func<HttpRequest, HttpResponse, string> IdentifyCompany = (HttpRequest req, HttpResponse res) =>  {
    return "12345";  
} ;

public static Func<HttpRequest, HttpResponse, string> GetSessionToken = (HttpRequest req, HttpResponse res) => {
    return "23jdf0owekfmcn4u3qypxg09w4d8ayrcdx8nu2ng]s98y18cx98q3yhwmnhcfx43f";
};

public static Func<HttpRequest, HttpResponse, Dictionary<string, string>> GetMetadata = (HttpRequest req, HttpResponse res) => {
    Dictionary<string, string> metadata = new Dictionary<string, string>
    {
        { "email", "abc@email.com" },
        { "name", "abcdef" },
        { "image", "123" }
    };
    return metadata;
};

public static Func<HttpRequest, HttpResponse, bool> Skip = (HttpRequest req, HttpResponse res) => {
    string uri = new Uri(req.GetDisplayUrl()).ToString();
    if (uri.Contains("test"))
    {
        return true;
    }
    return false;
};

public static Func<EventModel, EventModel> MaskEventModel = (EventModel event_model) => {
    event_model.UserId = "masked_user_id";
    return event_model;
};

static public Dictionary<string, object> moesifOptions = new Dictionary<string, object>
{
    {"ApplicationId", "Your Application ID Found in Settings on Moesif"},
    {"LocalDebug", true},
    {"LogBody", true},
    {"ApiVersion", "1.0.0"},
    {"IdentifyUser", IdentifyUser},
    {"IdentifyCompany", IdentifyCompany},
    {"GetSessionToken", GetSessionToken},
    {"GetMetadata", GetMetadata},
    {"Skip", Skip},
    {"MaskEventModel", MaskEventModel}
};
```

## NET Framework installation

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

    public void Configuration(IAppBuilder app)
    {
        app.UseMiddleware<MoesifMiddleware>(moesifOptions);
    }
}
```

Your Moesif Application Id can be found in the [_Moesif Portal_](https://www.moesif.com/).
After signing up for a Moesif account, your Moesif Application Id will be displayed during the onboarding steps. 

You can always find your Moesif Application Id at any time by logging 
into the [_Moesif Portal_](https://www.moesif.com/), click on the top right menu,
 and then clicking _Installation_.

### .NET Framework Example
Checkout the [Moesif .NET Framework Example](https://github.com/Moesif/moesif-netframework-example)
using .NET Framwork 4.6.1

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

#### __`ApiVersion`__
(optional), _string_, api version associated with this particular event.

#### __`LocalDebug`__
_boolean_, set to true to print internal log messages for debugging SDK integration issues.

#### __`LogBody`__
_boolean_, default true. Set to false to not log the request and response body to Moesif.

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
public static Func<IOwinRequest, IOwinResponse, string> IdentifyUser = (IOwinRequest req, IOwinResponse res) =>
{
    return "my_user_id";
};

public static Func<IOwinRequest, IOwinResponse, string> IdentifyCompany = (IOwinRequest req, IOwinResponse res) => {
    return "my_company_id";
};

public static Func<IOwinRequest, IOwinResponse, string> GetSessionToken = (IOwinRequest req, IOwinResponse res) => {
    return "23jdf0owekfmcn4u3qypxg09w4d8ayrcdx8nu2ng]s98y18cx98q3yhwmnhcfx43f";
};

public static Func<IOwinRequest, IOwinResponse, Dictionary<string, object>> GetMetadata = (IOwinRequest req, IOwinResponse res) => {
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
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

public static Func<IOwinRequest, IOwinResponse, bool> Skip = (IOwinRequest req, IOwinResponse res) => {
string uri = req.Uri.ToString();
return uri.Contains("skip") ;
};

public static Func<EventModel, EventModel> MaskEventModel = (EventModel event_model) => {
    event_model.UserId = "masked_user_id";
    return event_model;
};

static public Dictionary<string, object> moesifOptions = new Dictionary<string, object>
{
    {"ApplicationId", "Your Application ID Found in Settings on Moesif"},
    {"LocalDebug", true},
    {"LogBody", true},
    {"ApiVersion", "1.0.0"},
    {"IdentifyUser", IdentifyUser},
    {"IdentifyCompany", IdentifyCompany},
    {"GetSessionToken", GetSessionToken},
    {"GetMetadata", GetMetadata},
    {"Skip", Skip},
    {"MaskEventModel", MaskEventModel}
};

```
## Update User

### UpdateUser method
A method is attached to the moesif middleware object to update the users profile or metadata.
The metadata field can be any custom data you want to set on the user. The `user_id` field is required.

```csharp
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
    {"string_field", "value_1"},
    {"number_field", 0},
    {"object_field", new Dictionary<string, string> {
        {"field_a", "value_a"},
        {"field_b", "value_b"}
        }
    }
};

Dictionary<string, object> user = new Dictionary<string, object>
{
    {"user_id", "csharpapiuser"},
    {"metadata", metadata},
};

MoesifMiddleware moesifMiddleware = new MoesifMiddleware(RequestDelegate next, Dictionary<string, object> moesifOptions)
moesifMiddleware.UpdateUser(user);
```

### UpdateUsersBatch method
A method is attached to the moesif middleware object to update the users profile or metadata in batch.
The metadata field can be any custom data you want to set on the user. The `user_id` field is required.

```csharp
List<Dictionary<string, object>> usersBatch = new List<Dictionary<string, object>>();
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
    {"string_field", "value_1"},
    {"number_field", 0},
    {"object_field", new Dictionary<string, string> {
        {"field_a", "value_a"},
        {"field_b", "value_b"}
        }
    }
};

Dictionary<string, object> userA = new Dictionary<string, object>
{
    {"user_id", "csharpapiuser"},
    {"metadata", metadata},
};

Dictionary<string, object> userB = new Dictionary<string, object>
{
    {"user_id", "csharpapiuser1"},
    {"modified_time", DateTime.UtcNow},
    {"metadata", metadata},
};

usersBatch.Add(userA);
usersBatch.Add(userB);

MoesifMiddleware moesifMiddleware = new MoesifMiddleware(RequestDelegate next, Dictionary<string, object> moesifOptions)
moesifMiddleware.UpdateUsersBatch(usersBatch);
```

## Update Company

### UpdateCompany method
A method is attached to the moesif middleware object to update the company profile or metadata.
The metadata field can be any custom data you want to set on the company. The `company_id` field is required.

```csharp
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
    {"string_field", "value_1"},
    {"number_field", 0},
    {"object_field", new Dictionary<string, string> {
        {"field_a", "value_a"},
        {"field_b", "value_b"}
        }
    }
};

Dictionary<string, object> company = new Dictionary<string, object>
{
    {"company_id", "csharpapicompany"},
    {"company_domain", "acmeinc.com"},
    {"metadata", metadata},
};

MoesifMiddleware moesifMiddleware = new MoesifMiddleware(RequestDelegate next, Dictionary<string, object> moesifOptions)
moesifMiddleware.UpdateCompany(company);
```

### UpdateCompaniesBatch method
A method is attached to the moesif middleware object to update the companies profile or metadata in batch.
The metadata field can be any custom data you want to set on the company. The `company_id` field is required.

```csharp
List<Dictionary<string, object>> companiesBatch = new List<Dictionary<string, object>>();
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
    {"string_field", "value_1"},
    {"number_field", 0},
    {"object_field", new Dictionary<string, string> {
        {"field_a", "value_a"},
        {"field_b", "value_b"}
        }
    }
};

Dictionary<string, object> companyA = new Dictionary<string, object>
{
    {"user_id", "csharpapicompany"},
    {"company_domain", "acmeinc.com"},
    {"metadata", metadata},
};

Dictionary<string, object> companyB = new Dictionary<string, object>
{
    {"user_id", "csharpapicompany1"},
    {"company_domain", "nowhere.com"},
    {"metadata", metadata},
};

companiesBatch.Add(companyA);
companiesBatch.Add(companyB);

MoesifMiddleware moesifMiddleware = new MoesifMiddleware(RequestDelegate next, Dictionary<string, object> moesifOptions)
moesifMiddleware.UpdateCompaniesBatch(companiesBatch);
```

## How to test

1. Manually clone the git repo
2. From terminal/cmd navigate to the root directory of the middleware.
3. Invoke 'Install-Package Moesif.Middleware'
4. Add your own application id to 'Moesif.Middleware.Test/MoesifMiddlewareTest.cs'. You can find your Application Id from [_Moesif Dashboard_](https://www.moesif.com/) -> _Top Right Menu_ -> _Installation_
5. The tests are contained in the Moesif.Middleware.Test project. In order to invoke these test cases, you will need NUnit 3.0 Test Adapter Extension for Visual Studio. Once the SDK is complied, the test cases should appear in the Test Explorer window. Here, you can click Run All to execute these test cases.

## Tested versions

Moesif has validated Moesif.Middleware against the following framework.

|               | Framework Version  |
| ------------- | -----------------  | 
| .NET Core     |         2.0        |
| .NET Framework |        4.6.1       |

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
