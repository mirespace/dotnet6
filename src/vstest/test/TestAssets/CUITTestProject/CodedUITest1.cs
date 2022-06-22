// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CodedUITestProject
{
    using System;
	using System.Collections.Generic;
	using System.Text.RegularExpressions;
	using System.Windows.Input;
	using System.Drawing;
	using Microsoft.VisualStudio.TestTools.UITesting;
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	using Microsoft.VisualStudio.TestTools.UITest.Extension;

    [CodedUITest]
    public class CodedUITestProject
    {
        [TestMethod]
        public void CodedUITestMethod1()
        {
            UITestControl.Desktop.DrawHighlight();
        }
    }
}
