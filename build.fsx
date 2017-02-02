//  __  __       _   _       _   _ ______ _______
// |  \/  |     | | | |     | \ | |  ____|__   __|
// | \  / | __ _| |_| |__   |  \| | |__     | |
// | |\/| |/ _` | __| '_ \  | . ` |  __|    | |
// | |  | | (_| | |_| | | |_| |\  | |____   | |
// |_|  |_|\__,_|\__|_| |_(_)_| \_|______|  |_|
//
// Math.NET Spatial - https://spatial.mathdotnet.com
// Copyright (c) Math.NET - Open Source MIT/X11 License
//
// FAKE build script, see http://fsharp.github.io/FAKE
//

// --------------------------------------------------------------------------------------
// PRELUDE
// --------------------------------------------------------------------------------------

#I "packages/build/FAKE/tools"
#r "packages/build/FAKE/tools/FakeLib.dll"

open Fake
open Fake.DocuHelper
open System
open System.IO

#load "build/build-framework.fsx"
open BuildFramework


// --------------------------------------------------------------------------------------
// PROJECT INFO
// --------------------------------------------------------------------------------------

// VERSION OVERVIEW

let spatialRelease = release "Math.NET Spatial" "RELEASENOTES.md"
let releases = [ spatialRelease ]
traceHeader releases


// CORE PACKAGES

let summary = "Math.NET Spatial, providing methods and algorithms for geometry computations in science, engineering and every day use."
let description = "Math.NET Spatial. "
let support = "Supports .Net 4.0 and Mono on Windows, Linux and Mac."
let tags = "math spatial geometry 2D 3D"

let libnet35 = "lib/net35"
let libnet40 = "lib/net40"
let libnet45 = "lib/net45"
let libpcl7 = "lib/portable-net45+netcore45+MonoAndroid1+MonoTouch1"
let libpcl47 = "lib/portable-net45+sl5+netcore45+MonoAndroid1+MonoTouch1"
let libpcl78 = "lib/portable-net45+netcore45+wp8+MonoAndroid1+MonoTouch1"
let libpcl259 = "lib/portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"
let libpcl328 = "lib/portable-net4+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"

let spatialPack =
    { Id = "Akomi.MathNet.Spatial"
      Release = spatialRelease
      Title = "Math.NET Spatial"
      Summary = summary
      Description = description + support
      Tags = tags
      Authors = [ "Christoph Ruegg"; "Johan Larsson" ]
      FsLoader = false
      Dependencies =
        [ { FrameworkVersion=""
            Dependencies=[ "MathNet.Numerics", GetPackageVersion "packages" "MathNet.Numerics" ] } ]
      Files =
        [ @"..\..\out\lib\Net40\Akomi.MathNet.Spatial.*", Some libnet40, None;
          @"..\..\src\Spatial\**\*.cs", Some "src/Common", None ] }

let coreBundle =
    { Id = spatialPack.Id
      Release = spatialRelease
      Title = spatialPack.Title
      Packages = [ spatialPack ] }


// --------------------------------------------------------------------------------------
// PREPARE
// --------------------------------------------------------------------------------------

Target "Start" DoNothing

Target "Clean" (fun _ ->
    CleanDirs [ "obj" ]
    CleanDirs [ "out/api"; "out/docs"; "out/packages" ]
    CleanDirs [ "out/lib/Net40" ]
    CleanDirs [ "out/test/Net40" ])

Target "ApplyVersion" (fun _ ->
    patchVersionInAssemblyInfo "src/Spatial" spatialRelease
    patchVersionInAssemblyInfo "src/SpatialUnitTests" spatialRelease)

Target "Prepare" DoNothing
"Start"
  =?> ("Clean", not (hasBuildParam "incremental"))
  ==> "ApplyVersion"
  ==> "Prepare"


// --------------------------------------------------------------------------------------
// BUILD
// --------------------------------------------------------------------------------------

Target "BuildMain" (fun _ -> build !! "Akomi.MathNet.Spatial.sln")
Target "BuildAll" (fun _ -> build !! "MathNet.Spatial.All.sln")

Target "Build" DoNothing
"Prepare"
  =?> ("BuildAll", hasBuildParam "all" || hasBuildParam "release")
  =?> ("BuildMain", not (hasBuildParam "all" || hasBuildParam "release" || hasBuildParam "net35"))
  ==> "Build"


// --------------------------------------------------------------------------------------
// TEST
// --------------------------------------------------------------------------------------

Target "Test" (fun _ -> test !! "out/test/**/*UnitTests*.dll")
"Build" ==> "Test"


// --------------------------------------------------------------------------------------
// PACKAGES
// --------------------------------------------------------------------------------------

Target "Pack" DoNothing

// ZIP

Target "Zip" (fun _ ->
    CleanDir "out/packages/Zip"
    coreBundle |> zip "out/packages/Zip" "out/lib" (fun f -> f.Contains("MathNet.Spatial.") || f.Contains("MathNet.Numerics.")))
"Build" ==> "Zip" ==> "Pack"

// NUGET

Target "NuGet" (fun _ ->
    CleanDir "out/packages/NuGet"
    if hasBuildParam "all" || hasBuildParam "release" then
        nugetPack coreBundle "out/packages/NuGet")
"Build" ==> "NuGet" ==> "Pack"


// --------------------------------------------------------------------------------------
// Documentation
// --------------------------------------------------------------------------------------

// DOCS

Target "CleanDocs" (fun _ -> CleanDirs ["out/docs"])

let extraDocs =
    [ "LICENSE.md", "License.md"
      "CONTRIBUTING.md", "Contributing.md"
      "CONTRIBUTORS.md", "Contributors.md" ]

Target "Docs" (fun _ ->
    provideDocExtraFiles extraDocs releases
    generateDocs true false)
Target "DocsDev" (fun _ ->
    provideDocExtraFiles extraDocs releases
    generateDocs true true)
Target "DocsWatch" (fun _ ->
    provideDocExtraFiles extraDocs releases
    use watcher = new FileSystemWatcher(DirectoryInfo("docs/content").FullName, "*.*")
    watcher.EnableRaisingEvents <- true
    watcher.Changed.Add(fun e -> generateDocs false true)
    watcher.Created.Add(fun e -> generateDocs false true)
    watcher.Renamed.Add(fun e -> generateDocs false true)
    watcher.Deleted.Add(fun e -> generateDocs false true)
    traceImportant "Waiting for docs edits. Press any key to stop."
    System.Console.ReadKey() |> ignore
    watcher.EnableRaisingEvents <- false
    watcher.Dispose())

"Build" ==> "CleanDocs" ==> "Docs"

"Start"
  =?> ("CleanDocs", not (hasBuildParam "incremental"))
  ==> "DocsDev"
  ==> "DocsWatch"


// API REFERENCE

Target "CleanApi" (fun _ -> CleanDirs ["out/api"])

Target "Api" (fun _ ->
    !! "out/lib/Net40/Akomi.MathNet.Spatial.dll"
    |> Docu (fun p ->
        { p with
            ToolPath = "tools/docu/docu.exe"
            TemplatesPath = "tools/docu/templates/"
            TimeOut = TimeSpan.FromMinutes 10.
            OutputPath = "out/api/" }))

"Build" ==> "CleanApi" ==> "Api"


// --------------------------------------------------------------------------------------
// Publishing
// Requires permissions; intended only for maintainers
// --------------------------------------------------------------------------------------

Target "PublishTag" (fun _ -> publishReleaseTag "Math.NET Spatial" "" spatialRelease)

Target "PublishMirrors" (fun _ -> publishMirrors ())
Target "PublishDocs" (fun _ -> publishDocs spatialRelease)
Target "PublishApi" (fun _ -> publishApi spatialRelease)

Target "PublishArchive" (fun _ -> publishArchive "out/packages/Zip" "out/packages/NuGet" [coreBundle])

Target "PublishNuGet" (fun _ -> !! "out/packages/NuGet/*.nupkg" -- "out/packages/NuGet/*.symbols.nupkg" |> publishNuGet)

Target "Publish" DoNothing
Dependencies "Publish" [ "PublishTag"; "PublishDocs"; "PublishApi"; "PublishArchive"; "PublishNuGet" ]


// --------------------------------------------------------------------------------------
// Default Targets
// --------------------------------------------------------------------------------------

Target "All" DoNothing
Dependencies "All" [ "Pack"; "Docs"; "Api"; "Test" ]

RunTargetOrDefault "Test"
