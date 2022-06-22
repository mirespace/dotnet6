' Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

Imports System
Imports System.Runtime.InteropServices

Namespace Microsoft.VisualStudio.Editors.Interop

    <ComImport(), System.Runtime.InteropServices.Guid("A0EBEE86-72AD-4a29-8C0E-D745F843BE1D"), _
    InterfaceTypeAttribute(ComInterfaceType.InterfaceIsDual), _
    CLSCompliant(False)> _
    Friend Interface IVsBuildEventCommandLineDialogService
        <PreserveSig()> _
        Function EditCommandLine(<InAttribute,MarshalAs(UnmanagedType.BStr)>ByVal WindowText As String, _
								<InAttribute,MarshalAs(UnmanagedType.BStr)>ByVal HelpID As String, _
								<InAttribute,MarshalAs(UnmanagedType.BStr)>ByVal OriginalCommandLine As String, _
								<InAttribute>ByVal MacroProvider as IVsBuildEventMacroProvider, _
								<OutAttribute,MarshalAs(UnmanagedType.BStr)>ByRef Result as String) As Integer
    End Interface

End Namespace
