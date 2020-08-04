﻿module Program

open System
open System.IO
open System.Text
open Fake.IO
open Fake.Core

let path xs = Path.Combine(Array.ofList xs)

let solutionRoot = Files.findParent __SOURCE_DIRECTORY__ "Snowflaqe.sln";

let src = path [ solutionRoot; "src" ]
let tests = path [ solutionRoot; "tests" ]

let test() =
    if Shell.Exec(Tools.dotnet, "run", tests) <> 0
    then failwith "tests failed"

let build() =
    if Shell.Exec(Tools.dotnet, "build --configuration Release", solutionRoot) <> 0
    then failwith "tests failed"

let pack() =
    Shell.deleteDir (path [ "src"; "bin" ])
    Shell.deleteDir (path [ "src"; "obj" ])
    if Shell.Exec(Tools.dotnet, "pack --configuration Release", src) <> 0 then
        failwith "Pack failed"
    else
        let outputPath = path [ src; "bin"; "Release" ]
        if Shell.Exec(Tools.dotnet, sprintf "tool install -g snowflaqe --add-source %s" outputPath) <> 0
        then failwith "Local install failed"

let publish() =
    if Shell.Exec(Tools.dotnet, "pack --configuration Release", src) <> 0 then
        failwith "Pack failed"
    else
        let nugetKey =
            match Environment.environVarOrNone "NUGET_KEY" with
            | Some nugetKey -> nugetKey
            | None -> failwith "The Nuget API key must be set in a NUGET_KEY environmental variable"

        let nugetPath =
            Directory.GetFiles(path [ src; "bin"; "Release" ])
            |> Seq.head
            |> Path.GetFullPath

        if Shell.Exec(Tools.dotnet, sprintf "nuget push %s -s nuget.org -k %s" nugetPath nugetKey, src) <> 0
        then failwith "Publish failed"

let buildCraftSchema() =
    if Shell.Exec(Tools.dotnet, "run -- --config ../samples/craft-cms/snowflaqe.json --generate", path [ solutionRoot; "src" ]) <> 0 then
        failwith "Running Fable generation failed"
    else
        if Shell.Exec(Tools.dotnet, "build", path [ solutionRoot; "samples"; "craft-cms"; "output" ]) <> 0
        then failwith "Could not build generated CraftCMS"

let buildGithub() =
    if Shell.Exec(Tools.dotnet, "run -- --config ../samples/github/snowflaqe.json --generate", path [ solutionRoot; "src" ]) <> 0
    then failwith "Failed to generate Github client"
    elif Shell.Exec(Tools.dotnet, "build", path [ solutionRoot; "samples"; "github"; "output" ]) <> 0
    then failwith "Failed to build the generated Github project"

let integration() =
    if Shell.Exec(Tools.dotnet, "run -- --generate", path [ solutionRoot; "src" ]) <> 0 then
        failwith "Running Fable generation failed"
    else
        if Shell.Exec(Tools.dotnet, "build", path [ solutionRoot; "src"; "output" ]) <> 0
        then failwith "Building generated Fable project failed"
        else
            if Shell.Exec(Tools.dotnet, "run -- --config ./snowflaqe-fsharp.json --generate", path [ solutionRoot; "src" ]) <> 0 then
                failwith "Running FSharp generation failed"
            else
            if Shell.Exec(Tools.dotnet, "build", path [ solutionRoot; "src"; "output" ]) <> 0
            then failwith "Building generated FSharp project failed"
            else
                if Shell.Exec(Tools.dotnet, "run -- --config ./snowflaqe-shared.json --generate", path [ solutionRoot; "src" ]) <> 0 then
                    failwith "Running Shared project generation failed"
                else
                    let output = List.sum [
                        Shell.Exec(Tools.dotnet, "build", path [ solutionRoot; "src"; "output"; "shared" ])
                        Shell.Exec(Tools.dotnet, "build", path [ solutionRoot; "src"; "output"; "fable" ])
                        Shell.Exec(Tools.dotnet, "build", path [ solutionRoot; "src"; "output"; "dotnet" ])
                    ]

                    if output <> 0 then failwith "Building generated shared projects failed"
                    else
                        buildGithub()
                        buildCraftSchema()


[<EntryPoint>]
let main (args: string[]) =
    Console.InputEncoding <- Encoding.UTF8
    Console.OutputEncoding <- Encoding.UTF8
    Console.WriteLine(Swag.logo)
    try
        match args with
        | [| "build"   |] -> build()
        | [| "test"    |] -> test()
        | [| "pack"    |] -> pack()
        | [| "publish" |] -> publish()
        | [| "integration" |] -> integration()
        | [| "build-craft" |] -> buildCraftSchema()
        | [| "build-github" |] -> buildGithub()

        | _ -> printfn "Unknown args %A" args
        0
    with ex ->
        printfn "%A" ex
        1