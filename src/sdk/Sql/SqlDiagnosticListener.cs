﻿//-----------------------------------------------------------------------------
// <copyright file="SqlDiagnosticListener.cs" company="Amazon.com">
//      Copyright 2020 Amazon.com, Inc. or its affiliates. All Rights Reserved.
//
//      Licensed under the Apache License, Version 2.0 (the "License").
//      You may not use this file except in compliance with the License.
//      A copy of the License is located at
//
//      http://aws.amazon.com/apache2.0
//
//      or in the "license" file accompanying this file. This file is distributed
//      on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
//      express or implied. See the License for the specific language governing
//      permissions and limitations under the License.
// </copyright>
//-----------------------------------------------------------------------------

#if !NET45
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Diagnostic listener for processing Sql query for System.Data.SqlClient and Microsoft.Data.SqlClient
    /// </summary>
    public class SqlDiagnosticListener : DiagnosticListenerBase
    {
        private static readonly Logger _logger = Logger.GetLogger(typeof(SqlDiagnosticListener));

        internal override string Name => "SqlClientDiagnosticListener";

        private static readonly ConcurrentDictionary<DbCommand, Subsegment> CurrentDbCommands = new ConcurrentDictionary<DbCommand, Subsegment>();

        protected override void OnEvent(KeyValuePair<string, object> value)
        {
            try
            {
                switch (value.Key)
                {
                    case "Microsoft.Data.SqlClient.WriteCommandBefore":
                        { 
                            OnEventStart(value.Value);   
                        }
                        break;
                    case "System.Data.SqlClient.WriteCommandBefore":
                        {
                            OnEventStart(value.Value);
                        }
                        break;
                    case "Microsoft.Data.SqlClient.WriteCommandAfter":
                        {
                            OnEventStop(value.Value);
                        }
                        break;
                    case "System.Data.SqlClient.WriteCommandAfter":
                        {
                            OnEventStop(value.Value);
                        }
                        break;
                    case "Microsoft.Data.SqlClient.WriteCommandError":
                        {
                            OnEventException(value.Value);
                        }
                        break;
                    case "System.Data.SqlClient.WriteCommandError":
                        {
                            OnEventException(value.Value);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Invalid diagnostic source key ({0})", value.Key);
            }
        }

        private void OnEventStart(object value)
        {
            // This class serves for tracing Sql command from both System.Data.SqlClient and Microsoft.Data.SqlClient and using fetch property works
            // fot both of these two cases 
            var command = AgentUtil.FetchPropertyUsingReflection(value, "Command");
            if (command is DbCommand dbcommand)
            {
                // Skip processing EntityFramework Core request
                if (SqlRequestUtil.IsTraceable() && CurrentDbCommands.TryAdd(dbcommand, null))
                {
                    SqlRequestUtil.BeginSubsegment(dbcommand);
                    SqlRequestUtil.ProcessCommand(dbcommand);
                }
            }
        }

        private void OnEventStop(object value)
        {
            // This class serves for tracing Sql command from both System.Data.SqlClient and Microsoft.Data.SqlClient and using fetch property works
            // fot both of these two cases 
            var command = AgentUtil.FetchPropertyUsingReflection(value, "Command");
            if (command is DbCommand dbcommand)
            {
                if (CurrentDbCommands.TryRemove(dbcommand, out _))
                {
                    SqlRequestUtil.EndSubsegment();
                }
            }
        }

        private void OnEventException(object value)
        {
            // This class serves for tracing Sql command from both System.Data.SqlClient and Microsoft.Data.SqlClient and using fetch property works
            // fot both of these two cases 
            var command = AgentUtil.FetchPropertyUsingReflection(value, "Command");
            var exc = AgentUtil.FetchPropertyUsingReflection(value, "Exception");
            if (command is DbCommand dbcommand && exc is Exception exception)
            {
                if (CurrentDbCommands.TryRemove(dbcommand, out _))
                {
                    SqlRequestUtil.ProcessException(exception);
                    SqlRequestUtil.EndSubsegment();
                }
            }
        }
    }
}
#endif
