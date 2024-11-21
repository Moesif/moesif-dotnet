# Moesif Middleware for .NET

by [Moesif](https://moesif.com), the [API analytics](https://www.moesif.com/features/api-analytics) and [API monetization](https://www.moesif.com/solutions/metered-api-billing) platform.

[![Built For][ico-built-for]][link-built-for]
[![Latest Version][ico-version]][link-package]
[![Software License][ico-license]][link-license]
[![Source Code][ico-source]][link-source]

Moesif .NET middleware SDK automatically logs incoming and outgoing API calls and sends them to [Moesif](https://www.moesif.com) for API analytics and monitoring.
This middleware allows you to integrate Moesif's API analytics and
API monetization features into your .NET applications with minimal configuration.

> If you're new to Moesif, see [our Getting Started](https://www.moesif.com/docs/) resources to quickly get up and running.

## Prerequisites

Before using this middleware, make sure you have the following:

- [An active Moesif account](https://moesif.com/wrap)
- [A Moesif Application ID](#get-your-moesif-application-id)

### Get Your Moesif Application ID

After you log into [Moesif Portal](https://www.moesif.com/wrap), you can get your Moesif Application ID during the onboarding steps. You can always access the Application ID any time by following these steps from Moesif Portal after logging in:

1. Select the account icon to bring up the settings menu.
2. Select **Installation** or **API Keys**.
3. Copy your Moesif Application ID from the **Collector Application ID** field.
   <img class="lazyload blur-up" src="images/app_id.png" width="700" alt="Accessing the settings menu in Moesif Portal">

## Install the Middleware

Install the Nuget Package for the middleware:

```bash
Install-Package Moesif.Middleware
```

### Versions & Target Frameworks.
| Version   | Target Framework |
| --------- | ------------------- |
|  3.1.0 or above | .NET46 / .NET6 or above |
|  1.5.3 or below | .NET45 / .NET Core 2 or below |

Note: __We have deprecated support for .NET Core 2 or below__

Then jump to usage instructions for your specific framework:

- [.NET6 installation](#use-the-middleware-in-net60-and-higher)
- [.NET Core and .NET 5 installation](#use-the-middleware-in-net-core-and-net-50)
- [.NET Framework installation](#use-the-middleware-in-net-framework)

## Configure the Middleware

See the following to learn how to configure the middleware for your use case.

- [.NET Core configuration options](#net-core-configuration-options)
- [.NET Framework configuration options](#net-framework-configuration-options)

## Use the Middleware in .NET6.0 and higher

Please use Moesif.Middleware version `3.0.9` or higher for .NET6 or higher framework. Rest of instrusctions are same.

## Use the Middleware in .NET Core and .NET 5.0

Follow these instructions to use this middleware in .NET 5 or .NET Core 2.1 and higher:

1. In `Startup.cs` file in your project directory, add `app.UseMiddleware<MoesifMiddleware>(moesifOptions);` to the pipeline.
   To collect the most context, we recommend that you add the middleware after other middleware such as SessionMiddleware and AuthenticationMiddleware.
2. Add the middleware in your application:

```csharp
using Moesif.Middleware;

public class Startup {

    // moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
    Dictionary<string, object> moesifOptions = new Dictionary<string, object>
    {
        {"ApplicationId", "YOUR_MOESIF_APPLICATION_ID"},
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

Replace *`YOUR_MOESIF_APPLICATION_ID`* with [your Moesif Application ID](#get-your-moesif-application-id).

## Use the Middleware in .NET Framework

Follow these instructiosn to use the middleware in .NET Framework 4.5 and higher.

1. In `Startup.cs` file in your project directory, please add `app.Use<MoesifMiddleware>(moesifOptions);` to the pipeline.
   To collect the most context, it is recommended to add the middleware after other middleware such as SessionMiddleware and AuthenticationMiddleware.
2. Add the middleware to your application:

```csharp
using Moesif.Middleware;

public class Startup {

    // moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
    Dictionary<string, object> moesifOptions = new Dictionary<string, object>
    {
        {"ApplicationId", "YOUR_MOESIF_APPLICATION_ID"},
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

Replace *`YOUR_MOESIF_APPLICATION_ID`* with [your Moesif Application ID](#get-your-moesif-application-id).

> If your app uses Windows Communication Foundation (WCF), set [DisableStreamOverride](#disablestreamoverride) to true

### Add OWIN Dependencies

#### IIS integrated pipeline

If your .NET application runs on IIS or Visual Studio IIS Express using integrated mode, install the OWIN SystemWeb package if not done so already.

```bash
Install-Package Microsoft.Owin.Host.SystemWeb
```

For more information, see [OWIN Middleware in the IIS integrated pipeline](https://docs.microsoft.com/en-us/aspnet/aspnet/overview/owin-and-katana/owin-middleware-in-the-iis-integrated-pipeline).

Moesif does not support IIS running in classic mode (backwards compatibility for IIS 6.0). Unless your app predates IIS 6.0 and requires classic mode, we recomend you switch to integrated mode.
{: .notice--primary}

#### Self-Host executable

While uncommon, if your application is a self-hosted executable that does not run on IIS, you may have to install the SelfHost package if not done so already:

1. [For .NET Web API applications](https://docs.microsoft.com/en-us/aspnet/web-api/overview/hosting-aspnet-web-api/use-owin-to-self-host-web-api):

    ```powershell
    Install-Package Microsoft.AspNet.WebApi.OwinSelfHost
    ```

2. [For all other .NET applications](https://docs.microsoft.com/en-us/aspnet/aspnet/overview/owin-and-katana/getting-started-with-owin-and-katana#self-host-owin-in-a-console-application):

    ```powershell
    Install-Package Microsoft.Owin.SelfHost -Pre
    ```

## Configuration Options

The following sections describe the middleware's configuration options for [.NET Core](#net-core-configuration-options) and [.NET Framework](#net-framework-configuration-options).

### .NET Core Configuration Options

The next sections describe the available configuration options for .NET Core. Here's a sample configuration using:

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

#### `ApplicationId` (Required)

|Data type|
|-|
|`string`|

A string that [identifies your application in Moesif](#get-your-moesif-application-id).

#### `Skip`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`boolean`|

Optional.

A function that takes a request and a response, and returns `true` if you want to skip this particular event.

#### `IdentifyUser`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes a request and a response, and returns a string that represents the user ID used by your system.

Moesif identifies users automatically. But if your setup differs from the standard implementations, provide this function to ensure user identification properly.

#### `IdentifyCompany`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes a request and a response, and returns a string that represents the company ID for this event.

#### `GetSessionToken`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes a request and a response, and returns a string that represents the session token for this event.

Similar to users and companies, Moesif tries to retrieve session tokens automatically. But if your setup differs from the standard, this function can be helpful for tying events together, and help you replay the events.

#### `GetMetadata`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`dictionary`|

Optional.

A function that returns an object that allows you to add custom metadata that will be associated with the event.

The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a virtual machine instance ID, a trace ID, or a tenant ID with the request.

#### `MaskEventModel`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(EventModel)`|`EventModel`|

A function that takes an a Moesif event model and returns an `EventModel` with desired data removed.

Use this if you prefer to write your own mask function. The return value must be a valid `EventModel` required by Moesif data ingestion API. For more information, see [Moesif C# API documentation](https://www.moesif.com/docs/api?csharp#).

For example:

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

#### `ApiVersion`

|Data type|
|-|
|`string`|

Optional.

The API version associated with this particular event.

#### `LocalDebug`

|Data type|
|-|
|`boolean`|

Set to `true` to print internal log messages for debugging SDK integration issues.

#### `LogBody`

|Data type|Default|
|-|-|
|`boolean`|`true`|

Set to `false` to not log the request and response body to Moesif.

#### `RequestMaxBodySize`

|Data type|Default|
|-|-|
|`Number`|`100000`|

The maximum request body size in bytes to log when sending the data to Moesif. Request body will not be logged if its size exceeds `RequestMaxBodySize`.

#### `ResponseMaxBodySize`

|Data type|Default|
|-|-|
|`Number`|`100000`|

The maximum response body size in bytes to log when sending the data to Moesif. Response body will not be logged if its size exceeds `RequestMaxBodySize`.

#### `AuthorizationHeaderName`

|Data type|
|-|
|`string`|

Optional.

Request header containing a bearer or basic token to extract user ID. Also, supports a comma-separated string. Moesif checks headers in order like `X-Api-Key`,`Authorization`, and so on.

#### `AuthorizationUserIdField`

|Data type|
|-|
|`string`|

Optional.

Field name in JWT or OpenId token's payload for identifying users. Only applicable if [`AuthorizationHeaderName`](#authorizationheadername) is set and is a bearer token.

#### `IsLambda`

|Data type|Default|
|-|-|
|`boolean`|`false`|

Set to `true` if integrating with AWS Lambda functions.

### `EnableBatching`

|Data type|Default|
|-|-|
|`boolean`|`true`|

Moesif logs events in batches. Set to `false` if you want to send the API events one by one. This option is reset to `false` if `IsLambda` true.

#### Capture Outgoing Requests in .NET Core

You can capture all outgoing API calls from your app to third parties like Stripe or to your own dependencies while using [System.Net.Http](https://docs.microsoft.com/en-us/dotnet/api/system.net.http?view=netframework-4.8) package.

The following snippet shows how to configure your application to start capturing outgoing calls:

```csharp
using System.Net.Http;
using Moesif.Middleware.Helpers;

// moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
MoesifCaptureOutgoingRequestHandler handler = new MoesifCaptureOutgoingRequestHandler(new HttpClientHandler(), moesifOptions);
HttpClient client = new HttpClient(handler);
```

The following configuration options are available for outgoing API calls. Several options are functions that take request and response as input arguments. These request and response objects correspond to [HttpRequestMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httprequestmessage) request and [HttpResponseMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httpresponsemessage) response objects respectively.

##### `GetMetadataOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`dictionary`|

Optional.

A function that returns an object that allows you
to add custom metadata associated with the event. The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a VM instance ID, a trace ID, or a tenant ID with the request.

##### `GetSessionTokenOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes an `HttpRequestMessage` and an `HttpResponseMessage`, and returns the session token string for this event.

Moesif tries to get the session token automatically, but if you setup differs from the standard, this function can be helpful for tying events together and help you replay the events.

##### `IdentifyUserOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes an `HttpRequestMessage` and an `HttpResponseMessage`, and returns a user ID string used by your system.

Moesif identifies users automatically. But if your setup differs from the standard implementations, provide this function to ensure user identification properly.

##### `IdentifyCompanyOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

A function that takes an `HttpRequestMessage` and an `HttpResponseMessage`, and returns company ID string for this event.

##### `SkipOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`boolean`|

Optional.

A function that takes an `HttpRequestMessage` and an `HttpResponseMessage`, and returns `true` if you want to skip this particular event.

##### `MaskEventModelOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(EventModel)`|`EventModel`|

Optional.

A function that takes a Moesif event model and returns an `EventModel` with desired data removed.

Use this if you prefer to write your own mask function. The return value must be a valid `EventModel` required by Moesif data ingestion API. For more information, see the [Moesif C# API documentation](https://www.moesif.com/docs/api?csharp#).

##### `LogBodyOutgoing`

|Data type|Default|
|-|-|
|`boolean`|`true`|

Set to `false` to not log the request and response body to Moesif.

### .NET Framework Configuration Options

The next sections describe the available configuration options for .NET Framework. Here's a sample configuration:

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

In this configuration options, the request and response objects passed in correspond to [`IOwinRequest`](https://docs.microsoft.com/en-us/previous-versions/aspnet/dn308194(v%3Dvs.113)) request and [`IOwinResponse`](https://docs.microsoft.com/en-us/previous-versions/aspnet/dn308204(v%3Dvs.113)) response objects respectively.

#### `ApplicationId` (Required)

|Data type|
|-|
|`string`|

A string that [identifies your application in Moesif](#get-your-moesif-application-id).

#### `Skip`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`boolean`|

Optional.

A function that takes a request and a response, and returns `true` if you want to skip this particular event.

#### `IdentifyUser`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes a request and a response, and returns a string that represents the user ID used by your system.

Moesif identifies users automatically. But if your setup differs from the standard implementations, provide this function to ensure user identification properly.

#### `IdentifyCompany`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes a request and a response, and returns a string that represents the company ID for this event.

#### `GetSessionToken`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes a request and a response, and returns a string that represents the session token for this event.

Similar to users and companies, Moesif tries to retrieve session tokens automatically. But if your setup differs from the standard, this function can be helpful for tying events together, and help you replay the events.

#### `GetMetadata`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`dictionary`|

Optional.

A function that returns an object that allows you to add custom metadata that will be associated with the event.

The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a virtual machine instance ID, a trace ID, or a tenant ID with the request.

#### `MaskEventModel`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(EventModel)`|`EventModel`|

A function that takes an a Moesif event model and returns an `EventModel` with desired data removed.

Use this if you prefer to write your own mask function. The return value must be a valid `EventModel` required by Moesif data ingestion API. For more information, see [Moesif C# API documentation](https://www.moesif.com/docs/api?csharp#).

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

#### `ApiVersion`

|Data type|
|-|
|`string`|

Optional.

The API version associated with this particular event.

#### `LocalDebug`

|Data type|
|-|
|`boolean`|

Set to `true` to print internal log messages for debugging SDK integration issues.

#### `LogBody`

|Data type|Default|
|-|-|
|`boolean`|`true`|

Set to `false` to not log the request and response body to Moesif.

#### `DisableStreamOverride`

|Data type|
|-|
|`boolean`|

Set to `true` to disable overriding the request body stream. This is required if your app is using Windows Communication Foundation (WCF). Otherwise, you may experience issues when your business logic accesses the request body.

#### `AuthorizationHeaderName`

|Data type|
|-|
|`string`|

Optional.

Request header containing a bearer or basic token to extract user ID. Also, supports a comma-separated string. Moesif checks headers in order like `X-Api-Key`,`Authorization`, and so on.

#### `AuthorizationUserIdField`

|Data type|
|-|
|`string`|

Optional.

Field name in JWT or OpenId token's payload for identifying users. Only applicable if [`AuthorizationHeaderName`](#authorizationheadername) is set and is a bearer token.

#### Capture Outgoing Requests in .NET Core

You can capture all outgoing API calls from your app to third parties like Stripe or to your own dependencies while using [System.Net.Http](https://docs.microsoft.com/en-us/dotnet/api/system.net.http?view=netframework-4.8) package.

The following snippet shows how to configure your application to start capturing outgoing calls:

```csharp
using System.Net.Http;
using Moesif.Middleware.Helpers;

// moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
MoesifCaptureOutgoingRequestHandler handler = new MoesifCaptureOutgoingRequestHandler(new HttpClientHandler(), moesifOptions);
HttpClient client = new HttpClient(handler);
```

The following configuration options are available for outgoing API calls. Several options are functions that take request and response as input arguments. These request and response objects correspond to [HttpRequestMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httprequestmessage) request and [HttpResponseMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httpresponsemessage) response objects respectively.

##### `GetMetadataOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`dictionary`|

Optional.

A function that returns an object that allows you
to add custom metadata associated with the event. The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a VM instance ID, a trace ID, or a tenant ID with the request.

##### `GetSessionTokenOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes an `HttpRequestMessage` and an `HttpResponseMessage`, and returns the session token string for this event.

Moesif tries to get the session token automatically, but if you setup differs from the standard, this function can be helpful for tying events together and help you replay the events.

##### `IdentifyUserOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

Optional.

A function that takes an `HttpRequestMessage` and an `HttpResponseMessage`, and returns a user ID string used by your system.

Moesif identifies users automatically. But if your setup differs from the standard implementations, provide this function to ensure user identification properly.

##### `IdentifyCompanyOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`string`|

A function that takes an `HttpRequestMessage` and an `HttpResponseMessage`, and returns company ID string for this event.

##### `SkipOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(HttpRequest, HttpResponse)`|`boolean`|

Optional.

A function that takes an `HttpRequestMessage` and an `HttpResponseMessage`, and returns `true` if you want to skip this particular event.

##### `MaskEventModelOutgoing`

|Data type|Parameters|Return type|
|-|-|-|
|Function|`(EventModel)`|`EventModel`|

Optional.

A function that takes a Moesif event model and returns an `EventModel` with desired data removed.

Use this if you prefer to write your own mask function. The return value must be a valid `EventModel` required by Moesif data ingestion API. For more information, see the [Moesif C# API documentation](https://www.moesif.com/docs/api?csharp#).

##### `LogBodyOutgoing`

|Data type|Default|
|-|-|
|`boolean`|`true`|

Set to `false` to not log the request and response body to Moesif.

## Examples

### .NET Core Examples

See the [.NET Core examples](https://github.com/Moesif/moesif-netcore-example)
using .NET Core 2.0 and .NET Core 3.0

### .NET Framework Examples

- [.NET Framework Example](https://github.com/Moesif/moesif-netframework-example) using .NET Framework 4.6.1 and IIS
- [.NET Framework SelfHost Example](https://github.com/Moesif/moesif-aspnet-webapi-selfhost-example) using .NET Framework 4.6.1 _(SelfHost is uncommon)_

The following examples demonstrate how to add and update customer information.

### Update a Single User

To create or update a [user](https://www.moesif.com/docs/getting-started/users/) profile in Moesif, use the `UpdateUser` method.

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

The `metadata` field can contain any customer demographic or other info you want to store. Moesif only requires the `user_id` field.

For more information, see the function documentation in [Moesif C# API Reference](https://www.moesif.com/docs/api?csharp#update-a-user).

### Update Users in Batch

To update a list of [users](https://www.moesif.com/docs/getting-started/users/) in one batch, use the `UpdateUsersBatch` method.

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

The `metadata` field can contain any customer demographic or other info you want to store. Moesif only requires the `user_id` field.

This method is a convenient helper that calls the Moesif API library. For more information, see the function documentation in [Moesif C# API Reference](https://www.moesif.com/docs/api?csharp#update-users-in-batch).

### Update a Single Company

To update a single [company](https://www.moesif.com/docs/getting-started/companies/), use the `UpdateCompany` method.

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

The `metadata` field can contain any customer demographic or other info you want to store. Moesif only requires the `company_id` field.

This method is a convenient helper that calls the Moesif API library. For more information, see the function documentation in [Moesif C# API Reference](https://www.moesif.com/docs/api?csharp#update-a-company).

### Update Companies in Batch

To update a list of [companies](https://www.moesif.com/docs/getting-started/companies/) in one batch, use the `UpdateCompaniesBatch` method.

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

The `metadata` field can contain any customer demographic or other info you want to store. Moesif only requires the `company_id` field.

This method is a convenient helper that calls the Moesif API library. For more information, see the function documentation in [Moesif C# API Reference](https://www.moesif.com/docs/api?csharp#update-companies-in-batch).

## Troubleshoot

For a general troubleshooting guide that can help you solve common problems, see [Server Troubleshooting Guide](https://www.moesif.com/docs/troubleshooting/server-troubleshooting-guide/).

Other troubleshooting supports:

- [FAQ](https://www.moesif.com/docs/faq/)
- [Moesif support email](mailto:support@moesif.com)

The following sections discuss some specific troubleshooting scenarios.

### Issue Reading Request Body in WCF

Certain serializers for Windows Communication Foundation (WCF) may not correctly bind the request body when using logging middleware like Moesif.
If your app uses Windows Communication Foundation (WCF), you may find that your business logic has errors accessing the request body such as for `POST` and `PUT` requests.

To fix this, set the option [`DisableStreamOverride`](#disablestreamoverride) to `true`:

```csharp
Dictionary<string, object> moesifOptions = new Dictionary<string, object>
{
    {"ApplicationId", "Your Moesif Application Id"},
    {"DisableStreamOverride", true},
};
```

### Traditional Monolith Website broken

Some monolith apps which contain both a website and an API in the same app may issues when API logging middleware is enabled.
This is usually due to interactions with other custom middleware.

Since usually this custom middleware is enabled for the website only, the fix we recommend is to enable Moesif only for your API.

To do so, use `MapWhen` that only activates the middleware if the `Path` contains `/api`

```csharp
app.MapWhen(context => context.Request.Path.ToString().Contains("/api"), appBuilder =>
{

    appBuilder.Use<MoesifMiddleware>(new System.Collections.Generic.Dictionary<string, object> {
        {"ApplicationId", "Your Moesif Application Id"}
    });
});
```

### The Response Body Is Not Logged or Calls Are Jot Recieved in Moesif

Each ASP.NET Core server has an option called `AllowSynchronousIO` that toggles synchronous IO APIs such as `HttpRequest.Body.Read`, `HttpResponse.Body.Write`, and `Stream.Flush`. These APIs have previously caused thread starvation and app hangs, so starting from ASP.NET Core 3.0 Preview 3, they are disabled by default.

You  need to set the following option to ensure the response body is logged and to ensure all events are forwarded to Moesif:

- For .NET Core and .NET 5:

    ```csharp
        .ConfigureKestrel((context, options) => {
            options.AllowSynchronousIO = true;
        });
    ```

- For .NET Core and .NET 6+, when using a `WebApplicationBuilder` in `Program.cs`:

    ```csharp
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.AllowSynchronousIO = true;
    });
    ```

[See the .NET documentation](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnetcore#http-synchronous-io-disabled-in-all-servers) relating to the origins of the `AllowSynchronousIO` option. For more information about this workaround, see [.NET Core 3.0 AllowSynchronousIO Workaround](https://khalidabuhakmeh.com/dotnet-core-3-dot-0-allowsynchronousio-workaround).

## Ensuring Errors Handled by ExceptionHandler Are Logged

To capture the API calls handled by `ExceptionHandler`, make sure that the `app.UseMiddleware<MoesifMiddleware>(moesifOptions);` precedes the `app.UseExceptionHandler()` in the pipeline.

## How to Test

1. Manually clone the git repository.
2. From your terminal, navigate to the root directory of the middleware.
3. Run `Install-Package Moesif.Middleware`.
4. Add your [Moesif Application ID](#get-your-moesif-application-id) to `Moesif.Middleware.Test/MoesifMiddlewareTest.cs`.
5. The tests live in the `Moesif.Middleware.Test` project. In order to invoke these test cases, you need NUnit 3.0 Test Adapter Extension for Visual Studio. Once the SDK finishes compiling, the test cases should appear in the **Test Explorer** window. Here, you can click **Run All** to execute these test cases.

## Tested Versions

Moesif has validated this middleware against the following framework versions.

|    Framework   | Framework Version  |
| -------------- | -----------------  |
| .NET |5.0|
| .NET |6.0|
| .NET |7.0|
| .NET Core|2.0-3.0|
| .NET Framework MVC |4.6-4.7|
| .NET Framework Web API|4.6-4.7|
| .NET Framework Web API SelfHost|4.6-4.7|

### Last Supported Version for .NET 4.5

SDK version `1.3.25` supports .NET 4.5, which will be no longer supported. Please upgrade to .NET 4.6.1 or higher.

## Explore Other Integrations

Explore other integration options from Moesif:

- [Server integration options documentation](https://www.moesif.com/docs/server-integration//)
- [Client integration options documentation](https://www.moesif.com/docs/client-integration/)

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
