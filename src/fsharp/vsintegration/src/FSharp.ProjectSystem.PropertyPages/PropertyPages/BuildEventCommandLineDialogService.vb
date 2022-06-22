' Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

Option Strict On
Option Explicit On 

Imports Microsoft.VisualStudio.Shell
Imports System
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices

Namespace Microsoft.VisualStudio.Editors.PropertyPages

    '--------------------------------------------------------------------------
    ' AttributeEditingService:
    '   Attribute Editing Service Class. Implements SAttributeEditingService 
    '   exposed via the IAttributeEditingService interface.
    '--------------------------------------------------------------------------
    <CLSCompliant(False)> _
    Friend NotInheritable Class BuildEventCommandLineDialogService
        Implements Microsoft.VisualStudio.Editors.Interop.IVsBuildEventCommandLineDialogService

        Private m_serviceProvider As IServiceProvider

        Friend Sub New(ByVal sp As IServiceProvider)
            m_serviceProvider = sp
        End Sub

        Function EditCommandLine(ByVal WindowText As String, ByVal HelpID As String, ByVal OriginalCommandLine As String, ByVal MacroProvider As Microsoft.VisualStudio.Editors.Interop.IVsBuildEventMacroProvider, ByRef Result As String) As Integer _
            Implements Microsoft.VisualStudio.Editors.Interop.IVsBuildEventCommandLineDialogService.EditCommandLine

            Dim frm As New BuildEventCommandLineDialog
            Dim i As Integer
            Dim Count As Integer

            '// Initialize the title text
            frm.SetFormTitleText(WindowText)

            '// Initialize the command line
            frm.EventCommandLine = OriginalCommandLine

            '// Initialize helpTopicID
            If HelpID IsNot Nothing Then
                frm.HelpTopic = HelpID
            End If

            '// Initialize the token values
            Count = MacroProvider.GetCount()

            Dim Names(Count - 1) As String
            Dim Values(Count - 1) As String

            For i = 0 To Count - 1
                MacroProvider.GetExpandedMacro(i, Names(i), Values(i))
            Next

            frm.SetTokensAndValues(Names, Values)

            '// Show the form
            If (frm.ShowDialog(m_serviceProvider) = System.Windows.Forms.DialogResult.OK) Then
                Result = frm.EventCommandLine
                Return 0
            Else
                Result = OriginalCommandLine
                Return 1
            End If

        End Function
    End Class

End Namespace
