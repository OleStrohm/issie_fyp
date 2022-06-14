(*
    BuildView.fs

    View for catalogue in the right tab.
*)

module BuildView

open System
open Fulma
open Fulma.Extensions.Wikiki
open Fable.React
open Fable.React.Props
open DiagramStyle
open ModelType
open CommonTypes
open PopupView
open Sheet.SheetInterface
open DrawModelType
open FilesIO

open Node.ChildProcess
module node = Node.Api

let private menuItem styles label onClick =
    Menu.Item.li
        [ Menu.Item.IsActive false; Menu.Item.Props [ OnClick onClick; Style styles ] ]
        [ str label ]


let private makeRowForCompilationStage (name: string) (stage: SheetT.CompilationStage) =
    tr [] [
        th [] [str name]
        match stage with
        | SheetT.Completed t ->
                th [ Style [ BackgroundColor "green"] ] [str $"{t} seconds"]
        | SheetT.InProgress t ->
                th [ Style [ BackgroundColor "yellow"] ] [str $"{t} seconds"]
        | SheetT.Failed ->
                th [ Style [ BackgroundColor "red"] ] [str "XX"]
        | SheetT.Queued ->
                th [ Style [ BackgroundColor "gray"] ] [str "--"]
    ]

let verilogOutput (vType: Verilog.VMode) (model: Model) (dispatch: Msg -> Unit) =
    match FileMenuView.updateProjectFromCanvas model dispatch, model.Sheet.GetCanvasState() with
        | Some proj, state ->
            match model.UIState with
            | Some _ -> () // do nothing if in middle of I/O operation
            | None ->
                match Simulator.prepareSimulation proj.OpenFileName state proj.LoadedComponents with
                | Ok sim -> 
                    let path = FilesIO.pathJoin [| proj.ProjectPath; proj.OpenFileName + ".v" |]
                    printfn "should be compiling %s :: %s" proj.ProjectPath proj.OpenFileName
                    match tryCreateFolder <| pathJoin [| proj.ProjectPath; "/build" |] with
                    // TODO: No way to check for existence
                    //| Error e -> printfn "Couldn't make build folder: %s" e
                    | _ -> 
                        try
                            let verilog = Verilog.getVerilog vType sim.FastSim
                            printfn "%s" verilog
                            FilesIO.writeFile path verilog
                        with
                        | e ->
                            printfn $"Error in Verilog output: {e.Message}"
                            Error e.Message
                        |> (function
                            | Ok () -> ()//Sheet (SheetT.Msg.StartCompiling (proj.ProjectPath, proj.OpenFileName)) |> dispatch
                            | Error e -> ()//oh no
                            )
                | Error simError ->
                   printfn $"Error in simulation prevents verilog output {simError.Msg}"
        | _ -> ()

let viewBuild model dispatch =
        let viewCatOfModel = fun model ->                 
            let styles = 
                match model.Sheet.Action with
                | SheetT.InitialisedCreateComponent _ -> [Cursor "grabbing"]
                | _ -> []

            let catTip1 name func (tip:string) = 
                let react = menuItem styles name func
                div [ HTMLAttr.ClassName $"{Tooltip.ClassName} {Tooltip.IsMultiline}"
                      Tooltip.dataTooltip tip
                      Style styles
                    ]
                    [ react ]
            Menu.menu [Props [Class "py-1"; Style styles]]  [
                    Button.button
                        [ 
                            Button.Color IsPrimary;
                            Button.OnClick (fun _ -> verilogOutput Verilog.VMode.ForSynthesis model dispatch);
                        ]
                        [ str "create verilog" ]
                    if (model.Sheet.Compiling) then
                        Button.button
                            [ 
                                Button.Color IsDanger;
                                Button.OnClick (fun _ -> Sheet (SheetT.Msg.StopCompilation) |> dispatch);
                            ]
                            [ str "Stop building" ]
                    else
                        Button.button
                            [ 
                                Button.Color IsSuccess;
                                Button.OnClick (fun _ -> ());//Sheet (SheetT.Msg.StartCompiling) |> dispatch);
                            ]
                            [ str "Build and upload" ]

                    br []; br []
                    Table.table [
                        Table.IsFullWidth
                        Table.IsBordered
                    ] [
                        thead [] [ tr [] [
                            th [] [str "Stage"]
                            th [] [str "Progress"]
                        ] ]
                        tbody [] [
                            makeRowForCompilationStage "Synthesis" model.Sheet.CompilationStatus.Synthesis
                            makeRowForCompilationStage "Place And Route" model.Sheet.CompilationStatus.PlaceAndRoute
                            makeRowForCompilationStage "Generate" model.Sheet.CompilationStatus.Generate
                            makeRowForCompilationStage "Upload" model.Sheet.CompilationStatus.Upload
                        ]
                    ]

                    Button.button
                        [ 
                            Button.Color IsSuccess;
                            Button.OnClick (fun _ -> Sheet (SheetT.Msg.DebugSingleStep) |> dispatch);
                        ]
                        [ str "Step" ]
                    Button.button
                        [ 
                            Button.Color IsSuccess;
                            Button.OnClick (fun _ -> Sheet (SheetT.Msg.DebugRead 1) |> dispatch);
                        ]
                        [ str "Read" ]
                    Button.button
                        [ 
                            Button.Color IsSuccess;
                            Button.OnClick (fun _ -> Sheet (SheetT.Msg.DebugConnect) |> dispatch);
                        ]
                        [ str "Connect" ]
                    br [];
                    div [] [
                        str ([0..7]
                             |> List.rev
                             |> List.map (fun i -> (model.Sheet.DebugData / (pown 2 i)) % 2)
                             |> List.map (fun b -> b.ToString())
                             |> String.concat "")
                    ]
                ]

        (viewCatOfModel) model 
