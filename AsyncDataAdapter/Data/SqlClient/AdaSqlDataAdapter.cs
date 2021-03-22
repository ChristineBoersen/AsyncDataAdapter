//------------------------------------------------------------------------------
// <copyright file="SqlDataAdapter.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">[....]</owner>
// <owner current="true" primary="false">[....]</owner>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

using Microsoft.Data.SqlClient;

namespace AsyncDataAdapter.SqlClient
{
    [
    DefaultEvent("RowUpdated"),
    // TODO: ToolboxItem("Microsoft.VSDesigner.Data.VS.SqlDataAdapterToolboxItem, " + AssemblyRef.MicrosoftVSDesigner),
    // TODO: Designer("Microsoft.VSDesigner.Data.VS.SqlDataAdapterDesigner, " + AssemblyRef.MicrosoftVSDesigner)
    ]
    public sealed class AdaSqlDataAdapter : AdaDbDataAdapter, /*IDbDataAdapter, */ICloneable
    {

        static private readonly object EventRowUpdated = new object();
        static private readonly object EventRowUpdating = new object();
        
        private ISqlCommandSet _commandSet;
        private int _updateBatchSize = 1;

        public AdaSqlDataAdapter() : base()
        {
            GC.SuppressFinalize(this);
        }

        public AdaSqlDataAdapter(SqlCommand selectCommand) : this()
        {
            SelectCommand = selectCommand;
        }

        public AdaSqlDataAdapter(string selectCommandText, string selectConnectionString) : this()
        {
            SqlConnection connection = new SqlConnection(selectConnectionString);
            SelectCommand = new SqlCommand(selectCommandText, connection);
        }

        public AdaSqlDataAdapter(string selectCommandText, SqlConnection selectConnection) : this()
        {
            SelectCommand = new SqlCommand(selectCommandText, selectConnection);
        }

        private AdaSqlDataAdapter(AdaSqlDataAdapter from) : base(from)
        { // Clone
            GC.SuppressFinalize(this);
        }

        [
        DefaultValue(null),
        // TODO:  Editor("Microsoft.VSDesigner.Data.Design.DBCommandEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        // TODO:  ResCategoryAttribute(Res.DataCategory_Update),
        // TODO:  ResDescriptionAttribute(Res.DbDataAdapter_DeleteCommand),
        ]
        new public SqlCommand DeleteCommand
        {
            get { return (SqlCommand)base.DeleteCommand; }
            set { base.DeleteCommand = value; }
        }

        //IDbCommand IDbDataAdapter.DeleteCommand
        //{
        //    get { return _deleteCommand; }
        //    set { _deleteCommand = (SqlCommand)value; }
        //}

        [
        DefaultValue(null),
        // TODO: Editor("Microsoft.VSDesigner.Data.Design.DBCommandEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        // TODO: ResCategoryAttribute(Res.DataCategory_Update),
        // TODO:  ResDescriptionAttribute(Res.DbDataAdapter_InsertCommand),
        ]
        new public SqlCommand InsertCommand
        {
            get { return (SqlCommand)base.InsertCommand; }
            set { base.InsertCommand = value; }
        }

        //IDbCommand IDbDataAdapter.InsertCommand
        //{
        //    get { return _insertCommand; }
        //    set { _insertCommand = (SqlCommand)value; }
        //}

        [
        DefaultValue(null),
        // TODO:   Editor("Microsoft.VSDesigner.Data.Design.DBCommandEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        // TODO:  ResCategoryAttribute(Res.DataCategory_Fill),
        // TODO:  ResDescriptionAttribute(Res.DbDataAdapter_SelectCommand),
        ]
        new public SqlCommand SelectCommand
        {
            get { return (SqlCommand)base.SelectCommand; }
            set { base.SelectCommand = value; }
        }

        //IDbCommand IDbDataAdapter.SelectCommand
        //{
        //    get { return _selectCommand; }
        //    set { _selectCommand = (SqlCommand)value; }
        //}


        override public int UpdateBatchSize
        {
            get
            {
                return _updateBatchSize;
            }
            set
            {
                if (0 > value) // i.e. `value < 0`
                { // WebData 98157
                    throw new ArgumentOutOfRangeException(paramName: nameof(value), actualValue: value, message: nameof(this.UpdateBatchSize) + " value must be >= 0." );
                }
                _updateBatchSize = value;
            }
        }

        [
        DefaultValue(null),
        // TODO: Editor("Microsoft.VSDesigner.Data.Design.DBCommandEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        // TODO:  ResCategoryAttribute(Res.DataCategory_Update),
        // TODO:  ResDescriptionAttribute(Res.DbDataAdapter_UpdateCommand),
        ]
        new public SqlCommand UpdateCommand
        {
            get { return (SqlCommand)base.UpdateCommand; }
            set { base.UpdateCommand = value; }
        }

        //IDbCommand IDbDataAdapter.UpdateCommand
        //{
        //    get { return _updateCommand; }
        //    set { _updateCommand = (SqlCommand)value; }
        //}

        // TODO:[
        // TODO: ResCategoryAttribute(Res.DataCategory_Update),
        // TODO: ResDescriptionAttribute(Res.DbDataAdapter_RowUpdated),
        // TODO: ]
        public event SqlRowUpdatedEventHandler RowUpdated
        {
            add
            {
                Events.AddHandler(EventRowUpdated, value);
            }
            remove
            {
                Events.RemoveHandler(EventRowUpdated, value);
            }
        }

        // TODO: [
        // TODO: ResCategoryAttribute(Res.DataCategory_Update),
        // TODO: ResDescriptionAttribute(Res.DbDataAdapter_RowUpdating),
        // TODO:  ]
        public event SqlRowUpdatingEventHandler RowUpdating
        {
            add
            {
                SqlRowUpdatingEventHandler handler = (SqlRowUpdatingEventHandler)Events[EventRowUpdating];

                // MDAC 58177, 64513
                // prevent someone from registering two different command builders on the adapter by
                // silently removing the old one
                if ((null != handler) && (value.Target is DbCommandBuilder))
                {
                    SqlRowUpdatingEventHandler d = (SqlRowUpdatingEventHandler)ADP.FindBuilder(handler);
                    if (null != d)
                    {
                        Events.RemoveHandler(EventRowUpdating, d);
                    }
                }
                Events.AddHandler(EventRowUpdating, value);
            }
            remove
            {
                Events.RemoveHandler(EventRowUpdating, value);
            }
        }

        override protected int AddToBatch(IDbCommand command)
        {
            int commandIdentifier = _commandSet.CommandCount;
            _commandSet.Append((SqlCommand)command);
            return commandIdentifier;
        }

        override protected void ClearBatch()
        {
            _commandSet.Clear();
        }

        object ICloneable.Clone()
        {
            return new AdaSqlDataAdapter(this);
        }

        override protected RowUpdatedEventArgs CreateRowUpdatedEvent(DataRow dataRow, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        {
            return new SqlRowUpdatedEventArgs(dataRow, command, statementType, tableMapping);
        }

        override protected RowUpdatingEventArgs CreateRowUpdatingEvent(DataRow dataRow, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        {
            return new SqlRowUpdatingEventArgs(dataRow, command, statementType, tableMapping);
        }

        override protected int ExecuteBatch()
        {
            Debug.Assert(null != _commandSet && (0 < _commandSet.CommandCount), "no commands");
            // TODO:    Bid.CorrelationTrace("<sc.SqlDataAdapter.ExecuteBatch|Info|Correlation> ObjectID%d#, ActivityID %ls\n", ObjectID);
            return _commandSet.ExecuteNonQuery();
        }

        override protected IDataParameter GetBatchedParameter(int commandIdentifier, int parameterIndex)
        {
            Debug.Assert(commandIdentifier < _commandSet.CommandCount, "commandIdentifier out of range");
            Debug.Assert(parameterIndex < _commandSet.GetParameterCount(commandIdentifier), "parameter out of range");
            IDataParameter parameter = _commandSet.GetParameter(commandIdentifier, parameterIndex);
            return parameter;
        }

        override protected bool GetBatchedRecordsAffected(int commandIdentifier, out int recordsAffected, out Exception error)
        {
            Debug.Assert(commandIdentifier < _commandSet.CommandCount, "commandIdentifier out of range");
            return _commandSet.GetBatchedAffected(commandIdentifier, out recordsAffected, out error);
        }

        override protected void InitializeBatching()
        {
            _commandSet = SqlCommandSetFactory.CreateInstance();
            SqlCommand command = SelectCommand;
            if (null == command)
            {
                command = InsertCommand;
                if (null == command)
                {
                    command = UpdateCommand;
                    if (null == command)
                    {
                        command = DeleteCommand;
                    }
                }
            }
            if (command != null)
            {
                _commandSet.Connection     = command.Connection;
                _commandSet.Transaction    = command.Transaction;
                _commandSet.CommandTimeout = command.CommandTimeout;
            }
        }

        override protected void OnRowUpdated(RowUpdatedEventArgs value)
        {
            SqlRowUpdatedEventHandler handler = (SqlRowUpdatedEventHandler)Events[EventRowUpdated];
            if ((null != handler) && (value is SqlRowUpdatedEventArgs))
            {
                handler(this, (SqlRowUpdatedEventArgs)value);
            }
            base.OnRowUpdated(value);
        }

        override protected void OnRowUpdating(RowUpdatingEventArgs value)
        {
            SqlRowUpdatingEventHandler handler = (SqlRowUpdatingEventHandler)Events[EventRowUpdating];
            if ((null != handler) && (value is SqlRowUpdatingEventArgs))
            {
                handler(this, (SqlRowUpdatingEventArgs)value);
            }
            base.OnRowUpdating(value);
        }

        override protected void TerminateBatching()
        {
            if (null != _commandSet)
            {
                _commandSet.Dispose();
                _commandSet = null;
            }
        }
    }
}
