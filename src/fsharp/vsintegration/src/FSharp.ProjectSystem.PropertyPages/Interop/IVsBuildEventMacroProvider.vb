' Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

Imports System
Imports System.Runtime.InteropServices

Namespace Microsoft.VisualStudio.Editors.Interop

    <ComImport(), System.Runtime.InteropServices.Guid("ED895476-EF59-46fc-A985-581F58343E61"), _
    InterfaceTypeAttribute(ComInterfaceType.InterfaceIsDual), _
    CLSCompliant(False)> _
    Friend Interface IVsBuildEventMacroProvider
        Function GetCount() as Integer
		Sub GetExpandedMacro(<InAttribute>ByVal Index As Integer, _
					<OutAttribute,MarshalAs(UnmanagedType.BStr)>ByRef MacroName As String, _
					<OutAttribute,MarshalAs(UnmanagedType.BStr)>ByRef MacroValue as String)
    End Interface

End Namespace
