
//  Microsoft (R) .NET Framework IL Disassembler.  Version 4.6.1055.0
//  Copyright (c) Microsoft Corporation.  All rights reserved.



// Metadata version: v4.0.30319
.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 4:0:0:0
}
.assembly extern FSharp.Core
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )                         // .?_....:
  .ver 4:4:1:0
}
.assembly DoNotBoxStruct_ArrayOfArray_CSInterface
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.DoNotBoxStruct_ArrayOfArray_CSInterface
{
  // Offset: 0x00000000 Length: 0x0000026E
}
.mresource public FSharpOptimizationData.DoNotBoxStruct_ArrayOfArray_CSInterface
{
  // Offset: 0x00000278 Length: 0x000000A6
}
.module DoNotBoxStruct_ArrayOfArray_CSInterface.exe
// MVID: {59B1920A-FF24-C89E-A745-03830A92B159}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x015B0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed DoNotBoxStruct_ArrayOfArray_CSInterface
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static void  F<([mscorlib]System.IDisposable) T>(!!T[][] x) cil managed
  {
    // Code size       27 (0x1b)
    .maxstack  8
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 5,5 : 55,74 'C:\\GitHub\\dsyme\\visualfsharp\\tests\\fsharpqa\\Source\\CodeGen\\EmittedIL\\DoNotBoxStruct\\DoNotBoxStruct_ArrayOfArray_CSInterface.fs'
    IL_0000:  ldarg.0
    IL_0001:  ldc.i4.0
    IL_0002:  ldelem     !!T[]
    IL_0007:  ldc.i4.0
    IL_0008:  readonly.
    IL_000a:  ldelema    !!T
    IL_000f:  constrained. !!T
    IL_0015:  callvirt   instance void [mscorlib]System.IDisposable::Dispose()
    IL_001a:  ret
  } // end of method DoNotBoxStruct_ArrayOfArray_CSInterface::F

} // end of class DoNotBoxStruct_ArrayOfArray_CSInterface

.class private abstract auto ansi sealed '<StartupCode$DoNotBoxStruct_ArrayOfArray_CSInterface>'.$DoNotBoxStruct_ArrayOfArray_CSInterface
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $DoNotBoxStruct_ArrayOfArray_CSInterface::main@

} // end of class '<StartupCode$DoNotBoxStruct_ArrayOfArray_CSInterface>'.$DoNotBoxStruct_ArrayOfArray_CSInterface


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
