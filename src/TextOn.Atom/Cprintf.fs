﻿namespace TextOn.Atom

[<AutoOpen>]
module Cprint =
    /// Colored printf
    let cprintf c fmt = 
        Printf.kprintf
            (fun s ->
                let old = System.Console.ForegroundColor
                try
                    System.Console.ForegroundColor <- c
                    System.Console.Write s
                finally
                    System.Console.ForegroundColor <- old)
            fmt

    /// Colored printfn
    let cprintfn c fmt =
        cprintf c fmt
        printfn ""
