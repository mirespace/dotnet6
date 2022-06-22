﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.Unittest
{
    internal class BuildResultUtilities
    {
        public static TargetResult GetEmptyFailingTargetResult()
        {
            return new TargetResult(new TaskItem[0] { }, BuildResultUtilities.GetStopWithErrorResult());
        }

        public static TargetResult GetEmptySucceedingTargetResult()
        {
            return new TargetResult(new TaskItem[0] { }, BuildResultUtilities.GetSuccessResult());
        }

        public static TargetResult GetNonEmptySucceedingTargetResult()
        {
            return new TargetResult(new TaskItem[1] { new TaskItem("i", "v")}, BuildResultUtilities.GetSuccessResult());
        }

        public static WorkUnitResult GetSuccessResult()
        {
            return new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null);
        }

        public static WorkUnitResult GetSkippedResult()
        {
            return new WorkUnitResult(WorkUnitResultCode.Skipped, WorkUnitActionCode.Continue, null);
        }

        public static WorkUnitResult GetStopWithErrorResult()
        {
            return new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Stop, null);
        }

        public static WorkUnitResult GetStopWithErrorResult(Exception e)
        {
            return new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Stop, e);
        }

        public static WorkUnitResult GetContinueWithErrorResult()
        {
            return new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Continue, null);
        }
    }
}
