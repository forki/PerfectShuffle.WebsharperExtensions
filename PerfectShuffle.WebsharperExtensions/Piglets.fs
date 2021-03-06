﻿namespace PerfectShuffle.WebsharperExtensions.Piglets

open System
open WebSharper
open WebSharper.Piglets
open WebSharper.Html.Client
open WebSharper.JavaScript.Pervasives
open PerfectShuffle.WebsharperExtensions

[<JavaScript>]
module Controls =
  let DatePicker (stream:Stream<Option<DateTime>>) =
                       
    let config = JQueryUI.DatepickerConfiguration()
    config.DateFormat <- "d MM yy"
    let i =
      WebSharper.Html.Client.Tags.Input [WebSharper.Html.Client.Attr.Type "text"]
      |>! WebSharper.Html.Client.EventsPervasives.OnFocus(fun x -> (JQuery.JQuery.Of(x.Dom)).Blur() |> ignore<JQuery.JQuery>)
    let control =
      JQueryUI.Datepicker.New(i)
      |>! WebSharper.Html.Client.Operators.OnAfterRender(fun x ->
        x.Option(config)

        let initialDate = stream.Latest
        match initialDate with
        | Piglets.Result.Success(Some(date)) -> (x.SetDate(JavaScript.Date(date.Year, date.Month - 1, date.Day)))
        | _ -> ())

    let getControlDate() =
      try Some(control.GetDate().Self)
      with _ -> None

    match stream.Latest with
    | Piglets.Failure _ -> ()
    | Piglets.Success (Some(date)) ->
      match getControlDate() with
      | Some(controlDate) when controlDate = date -> ()
      | _ ->  control.SetDate(JavaScript.Date(date.Year, date.Month - 1, date.Day))
    | Piglets.Success _ ->
      i.Text <- ""
    stream.Subscribe(function
        | Piglets.Success (Some(date)) ->
          match getControlDate() with
          | Some(controlDate) when controlDate = date -> ()
          | _ ->  control.SetDate(JavaScript.Date(date.Year, date.Month - 1, date.Day))
        | Piglets.Success _ ->
          i.Text <- ""
        | Piglets.Failure _ -> ())
    |> ignore
    let ev () =
        let date = getControlDate()
        let v = Piglets.Success (date)
        if v <> stream.Latest then stream.Trigger v
    i.Body.AddEventListener("keyup", ev, true)
    i.Body.AddEventListener("change", ev, true)
    control.OnSelect(fun date _ -> ev())
                
    control :> Pagelet

[<JavaScript>]
module Piglet =  
  [<JavaScript>]
  let HideWhen (reader: Reader<'a>) (pred: Result<'a> -> bool) (element: Element) =            
    
      element |>! (fun el ->
        let jQuery = JQuery.JQuery.Of(el.Dom)
          
        let setVisibility isVisible =
          if isVisible
            then jQuery.Css("display", "") |> ignore //JQuery.Show() adds display:inline style which isn't always what we want!
            else jQuery.Hide() |> ignore

        setVisibility <| not (pred (reader.Latest))
        reader.Subscribe <| fun (x:Result<'a>) -> setVisibility (not (pred x))
        |> ignore)

  [<JavaScript>]
  let ShowWhen (reader: Reader<'a>) (pred: Result<'a> -> bool) (element: Element) = HideWhen reader (pred >> not) element

[<JavaScript>]
module Helpers =

  module Bootstrap =
    let InputWithLabel stream name labelText placeHolderText =
      [
        Label [Attr.Class "control-label"; Attr.For name; Text labelText]
        Controls.Input stream -< [Attr.Class "form-control"; Attr.Id name; Attr.Name name; AttrExtensions.Placeholder placeHolderText]
      ]

    let PasswordWithLabel stream name labelText placeHolderText =
      [
        Label [Attr.Class "control-label"; Attr.For name; Text labelText]
        Controls.Password stream -< [Attr.Class "form-control"; Attr.Id name; Attr.Name name; AttrExtensions.Placeholder placeHolderText]
      ]

    let FormGroupWithErrorHighlight = function Failure _ -> "form-group has-error" | Success _ -> "form-group has-success"

  /// Use to prevent a submit button from submitting a form when it is within <form> tags
  /// This is useful because we still get the benefit of 'enter' key hitting submit
  let preventDefaultFormSubmission (el:#Pagelet) =
    JQuery.JQuery.Of(el.Body).Bind("click", fun _ ev ->
      ev.PreventDefault()
      ).Ignore
    el

  let hasAtLeastOneErrorMessage result =
    match result with
    | Piglets.Success _ -> false
    | Piglets.Failure [] -> false
    | Piglets.Failure _ -> true

