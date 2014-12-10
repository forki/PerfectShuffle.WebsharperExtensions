﻿namespace PerfectShuffle.WebSharperExtensions

module CrossPlatform =
  let isRunningOnMono() = System.Type.GetType("Mono.Runtime") <> null

module Cookies =
  open System
  open IntelliFactory.WebSharper
  
  let getRequestCookies() =
    seq {
    for key in (System.Web.HttpContext.Current.Request.Cookies.Keys) do
      yield System.Web.HttpContext.Current.Request.Cookies.[key]
    }
  
  [<Inline("document.cookie=$str")>]
  let private setCookieString str = failwith "inline"
  
  /// Converts a date time into a UTC string compatible with document.cookie in javascript
  [<JavaScript>] 
  let toUtcString (dateTime:System.DateTime) =
    let dayOfWeek = 
      match dateTime.DayOfWeek with
      | DayOfWeek.Monday -> "Mon"
      | DayOfWeek.Tuesday -> "Tue"
      | DayOfWeek.Wednesday -> "Wed"
      | DayOfWeek.Thursday -> "Thu"
      | DayOfWeek.Friday -> "Fri"
      | DayOfWeek.Saturday -> "Sat"
      | DayOfWeek.Sunday -> "Sun"
      | _ -> failwith "Invalid day of week enum value"
    
    let day = if dateTime.Day <= 9 then "0" + dateTime.Day.ToString() else dateTime.Day.ToString()
    
    let month =
      match dateTime.Month with
      | 1 -> "Jan"
      | 2 -> "Feb"
      | 3 -> "Mar"
      | 4 -> "Apr"
      | 5 -> "May"
      | 6 -> "Jun"
      | 7 -> "Jul"
      | 8 -> "Aug"
      | 9 -> "Sep"
      | 10 -> "Oct"
      | 11 -> "Nov"
      | 12 -> "Dec"
      | _ -> failwith "Invalid month value"
  
    let year = dateTime.Year.ToString()
    
    let time = dateTime.Hour.ToString() + ":" + dateTime.Minute.ToString() + ":" + dateTime.Second.ToString()
  
    let utcTime = dayOfWeek + ", " + day + " " + month + " " + year + " " + time + " GMT"
    utcTime
  
  [<JavaScript>]
  let setPersistentCookie key value (expiry:System.DateTime) path : unit =
  
    let expiryString = toUtcString expiry
                
    let cookieString =
      key + "=" + value + "; expires=" + expiryString + "; path=" + path      
  
    setCookieString cookieString
  
  [<JavaScript>]
  let setSessionCookie key value path : unit =
    let cookieString =
      key + "=" + value + "; path=" + path
    
    setCookieString cookieString
  
  [<Inline("document.cookie = $name + '=; expires=Thu, 01 Jan 1970 00:00:01 GMT;'")>]
  let deleteCookie (name:string) : unit = failwith "inlined"
    
  type CookieExpiry =
  | Session
  | Persistent of DateTime

  type Cookie = {Name : string; Value : string; Expiry : CookieExpiry; Path : string}
   with    
     static member FromHttpCookie (cookie : System.Web.HttpCookie) =
       {Name = cookie.Name; Value = cookie.Value; Expiry = Persistent(cookie.Expires); Path = cookie.Path}

     [<JavaScript>]
     member this.SetCookie() =
       match this.Expiry with
       | Persistent expiry -> setPersistentCookie this.Name this.Value expiry this.Path
       | Session -> setSessionCookie this.Name this.Value this.Path

     [<JavaScript>]
     member this.Delete() =
       deleteCookie this.Name


module AspNetSecurity =
  open Cookies
  open System.Web.Security
  
  let createAuthenticationCookie email isPersistent expiration =
    let expiryTime = int (System.TimeSpan.FromDays(90.).TotalMinutes)
    let ticket = FormsAuthenticationTicket(2, email, System.DateTime.UtcNow, expiration, isPersistent, "") 
    let encryptedTicket = FormsAuthentication.Encrypt(ticket)

    let name = FormsAuthentication.FormsCookieName
    let expiry =
      match isPersistent with
      | true -> Persistent(expiration)
      | false -> Session

    {Name = name; Value = encryptedTicket; Expiry = expiry; Path = "/"}

  let authCookieName = FormsAuthentication.FormsCookieName

  let getLoggedInUser() =
    try
      match IntelliFactory.WebSharper.Sitelets.UserSession.GetLoggedInUser() with
      | Some(email) -> Some(email) 
      | None -> None
    with
      | :? System.NullReferenceException ->
        // Probably server was restarted and machine key changed so session no longer valid!
        IntelliFactory.WebSharper.Sitelets.UserSession.Logout()
        None

  let createSessionCSRFToken() =
    let key = "CSRFToken"
    let session = System.Web.HttpContext.Current.Session
    let sessionId = session.SessionID
        
    lock (session.SyncRoot) <| fun _ ->
      let token = session.[key] :?> string
      match token with
      | null ->
        // TODO: Might want to remove the dependency on another project, would perhaps
        // be nicer if this project was self-contained.
        let nonce = PerfectShuffle.Authentication.TokenGeneration.createRandomBase36Token 64

        let csrfToken = sessionId + nonce
        session.[key] <- csrfToken
        csrfToken
      | token when token.StartsWith(sessionId) -> token
      | _ -> failwith "Unexpected CSRF token found"

  let verifyCSRFToken token =
    let tokenCookie = System.Web.HttpContext.Current.Request.Cookies.["CSRFToken"]
    if (token <> tokenCookie.Value) then raise <| System.Security.SecurityException("CSRF Token invalid")    
    