﻿namespace TextOn.Atom

/// The definition of an attribute value, compiled, with meta-data.
type CompiledAttributeValue =
    {
        Value : string
        Condition : Condition
    }

/// The definition of an attribute, compiled, with meta-data.
type CompiledAttributeDefinition =
    {
        Name : string
        Index : int
        File : string
        StartLine : int
        EndLine : int
        Values : CompiledAttributeValue[]
    }
