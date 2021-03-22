
using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using AsyncDataAdapter.Internal;

namespace AsyncDataAdapter
{
    public abstract class AdaDataAdapter : Component /*, IDataAdapter */
    {
        private static readonly object EventFillError = new object();

        private bool _acceptChangesDuringUpdate            = true;
        private bool _acceptChangesDuringUpdateAfterInsert = true;
        private bool _continueUpdateOnError                = false;
        private bool _hasFillErrorHandler                  = false;
        private bool _returnProviderSpecificTypes          = false;
        private bool _acceptChangesDuringFill              = true;

        private LoadOption _fillLoadOption;

        private MissingMappingAction _missingMappingAction = System.Data.MissingMappingAction.Passthrough;
        private MissingSchemaAction  _missingSchemaAction  = System.Data.MissingSchemaAction.Add;

        private DataTableMappingCollection _tableMappings;

        private static int _objectTypeCount; // Bid counter
        internal readonly int _objectID = System.Threading.Interlocked.Increment(ref _objectTypeCount);

#if DEBUG
        // if true, we are asserting that the caller has provided a select command
        // which should not return an empty result set
        private static bool _debugHookNonEmptySelectCommand = false;
#endif

        [Conditional("DEBUG")]
        private static void AssertReaderHandleFieldCount(AdaDataReaderContainer readerHandler)
        {
#if DEBUG
            Debug.Assert(!_debugHookNonEmptySelectCommand || readerHandler.FieldCount > 0, "Scenario expects non-empty results but no fields reported by reader");
#endif
        }

        [Conditional("DEBUG")]
        private static void AssertSchemaMapping(AdaSchemaMapping mapping)
        {
#if DEBUG
            if (_debugHookNonEmptySelectCommand)
            {
                Debug.Assert(mapping != null && mapping.DataValues != null && mapping.DataTable != null, "Debug hook specifies that non-empty results are not expected");
            }
#endif
        }

        protected AdaDataAdapter() : base()
        { // V1.0.3300
            GC.SuppressFinalize(this);
        }

        protected AdaDataAdapter(AdaDataAdapter from) : base()
        { // V1.1.3300
            CloneFrom(from);
        }

        [
        DefaultValue(true),
        CategoryAttribute("Settins"),
        DescriptionAttribute("Accept changes during fill"),
        ]
        public bool AcceptChangesDuringFill
        { // V1.0.3300
            get
            {
                //Bid.Trace("<comm.DataAdapter.get_AcceptChangesDuringFill|API> %d#\n", ObjectID);
                return _acceptChangesDuringFill;
            }
            set
            {
                _acceptChangesDuringFill = value;
                //Bid.Trace("<comm.DataAdapter.set_AcceptChangesDuringFill|API> %d#, %d\n", ObjectID, value);
            }
        }

        [
        EditorBrowsableAttribute(EditorBrowsableState.Never)
        ]
        virtual public bool ShouldSerializeAcceptChangesDuringFill()
        {
            return (0 == _fillLoadOption);
        }

        [
        DefaultValue(true),
        CategoryAttribute("Settings"),
        DescriptionAttribute("Accept changes during update"),
        ]
        public bool AcceptChangesDuringUpdate
        {  // V1.2.3300, MDAC 74988
            get
            {
                //Bid.Trace("<comm.DataAdapter.get_AcceptChangesDuringUpdate|API> %d#\n", ObjectID);
                return _acceptChangesDuringUpdate;
            }
            set
            {
                _acceptChangesDuringUpdate = value;
                //Bid.Trace("<comm.DataAdapter.set_AcceptChangesDuringUpdate|API> %d#, %d\n", ObjectID, value);
            }
        }

        [
        DefaultValue(false),
        CategoryAttribute("Settings"),
        DescriptionAttribute("Continue update on error"),
        ]
        public bool ContinueUpdateOnError
        {  // V1.0.3300, MDAC 66900
            get
            {
                //Bid.Trace("<comm.DataAdapter.get_ContinueUpdateOnError|API> %d#\n", ObjectID);
                return _continueUpdateOnError;
            }
            set
            {
                _continueUpdateOnError = value;
                //Bid.Trace("<comm.DataAdapter.set_ContinueUpdateOnError|API> %d#, %d\n", ObjectID, value);
            }
        }

        [
        RefreshProperties(RefreshProperties.All),
        CategoryAttribute("Settings"),
        DescriptionAttribute("Fill load option"),
        ]
        public LoadOption FillLoadOption
        { // V1.2.3300
            get
            {
                //Bid.Trace("<comm.DataAdapter.get_FillLoadOption|API> %d#\n", ObjectID);
                LoadOption fillLoadOption = _fillLoadOption;
                return ((0 != fillLoadOption) ? _fillLoadOption : LoadOption.OverwriteChanges);
            }
            set
            {
                switch (value)
                {
                    case 0: // to allow simple resetting
                    case LoadOption.OverwriteChanges:
                    case LoadOption.PreserveChanges:
                    case LoadOption.Upsert:
                        _fillLoadOption = value;
                        //Bid.Trace("<comm.DataAdapter.set_FillLoadOption|API> %d#, %d{ds.LoadOption}\n", ObjectID, (int)value);
                        break;
                    default:
                        throw ADP.InvalidLoadOption(value);
                }
            }
        }

        [
        EditorBrowsableAttribute(EditorBrowsableState.Never)
        ]
        public void ResetFillLoadOption()
        {
            _fillLoadOption = 0;
        }

        [
        EditorBrowsableAttribute(EditorBrowsableState.Never)
        ]
        virtual public bool ShouldSerializeFillLoadOption()
        {
            return (0 != _fillLoadOption);
        }

        [
        DefaultValue(System.Data.MissingMappingAction.Passthrough),
        CategoryAttribute("Settings"),
        DescriptionAttribute("Missing mapping action"),
        ]
        public MissingMappingAction MissingMappingAction
        { // V1.0.3300
            get
            {
                //Bid.Trace("<comm.DataAdapter.get_MissingMappingAction|API> %d#\n", ObjectID);
                return _missingMappingAction;
            }
            set
            {
                switch (value)
                { // @perfnote: Enum.IsDefined
                    case MissingMappingAction.Passthrough:
                    case MissingMappingAction.Ignore:
                    case MissingMappingAction.Error:
                        _missingMappingAction = value;
                        //Bid.Trace("<comm.DataAdapter.set_MissingMappingAction|API> %d#, %d{ds.MissingMappingAction}\n", ObjectID, (int)value);
                        break;
                    default:
                        throw ADP.InvalidMissingMappingAction(value);
                }
            }
        }

        [
        DefaultValue(MissingSchemaAction.Add),
        CategoryAttribute("Settings"),
        DescriptionAttribute("Missing schema action"),
        ]
        public MissingSchemaAction MissingSchemaAction
        { // V1.0.3300
            get
            {
                //Bid.Trace("<comm.DataAdapter.get_MissingSchemaAction|API> %d#\n", ObjectID);
                return _missingSchemaAction;
            }
            set
            {
                switch (value)
                { // @perfnote: Enum.IsDefined
                    case MissingSchemaAction.Add:
                    case MissingSchemaAction.Ignore:
                    case MissingSchemaAction.Error:
                    case MissingSchemaAction.AddWithKey:
                        _missingSchemaAction = value;
                        //Bid.Trace("<comm.DataAdapter.set_MissingSchemaAction|API> %d#, %d{MissingSchemaAction}\n", ObjectID, (int)value);
                        break;
                    default:
                        throw ADP.InvalidMissingSchemaAction(value);
                }
            }
        }

        internal int ObjectID
        {
            get
            {
                return _objectID;
            }
        }

        [
        DefaultValue(false),
        CategoryAttribute("Settings"),
        DescriptionAttribute("Return provider specific types"),
        ]
        virtual public bool ReturnProviderSpecificTypes
        {
            get
            {
                //Bid.Trace("<comm.DataAdapter.get_ReturnProviderSpecificTypes|API> %d#\n", ObjectID);
                return _returnProviderSpecificTypes;
            }
            set
            {
                _returnProviderSpecificTypes = value;
                //Bid.Trace("<comm.DataAdapter.set_ReturnProviderSpecificTypes|API> %d#, %d\n", ObjectID, (int)value);
            }
        }

        [
        DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
        CategoryAttribute("Settings"),
        DescriptionAttribute("Table mappings"),
        ]
        public DataTableMappingCollection TableMappings
        { // V1.0.3300
            get
            {
                //Bid.Trace("<comm.DataAdapter.get_TableMappings|API> %d#\n", ObjectID);
                DataTableMappingCollection mappings = _tableMappings;
                if (null == mappings)
                {
                    mappings = CreateTableMappings();
                    if (null == mappings)
                    {
                        mappings = new DataTableMappingCollection();
                    }
                    _tableMappings = mappings;
                }
                return mappings; // constructed by base class
            }
        }

        //ITableMappingCollection IDataAdapter.TableMappings
        //{ // V1.0.3300
        //    get
        //    {
        //        return TableMappings;
        //    }
        //}

        virtual protected bool ShouldSerializeTableMappings()
        { // V1.0.3300, MDAC 65548
            return true; /*HasTableMappings();*/ // VS7 300569
        }

        protected bool HasTableMappings()
        { // V1.2.3300
            return ((null != _tableMappings) && (0 < TableMappings.Count));
        }

        [
        CategoryAttribute("Settings"),
        DescriptionAttribute("Fill error"),
        ]
        public event FillErrorEventHandler FillError
        { // V1.2.3300, DbDataADapter V1.0.3300
            add
            {
                _hasFillErrorHandler = true;
                Events.AddHandler(EventFillError, value);
            }
            remove
            {
                Events.RemoveHandler(EventFillError, value);
            }
        }

        [Obsolete("CloneInternals() has been deprecated.  Use the DataAdapter(DataAdapter from) constructor.  http://go.microsoft.com/fwlink/?linkid=14202")] // V1.1.3300, MDAC 81448
        // [System.Security.Permissions.PermissionSetAttribute(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")] // MDAC 82936
        virtual protected AdaDataAdapter CloneInternals()
        { // V1.0.3300
            AdaDataAdapter clone = (AdaDataAdapter)Activator.CreateInstance(GetType(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, null, CultureInfo.InvariantCulture, null);
            clone.CloneFrom(this);
            return clone;
        }

        private void CloneFrom(AdaDataAdapter from)
        {
            _acceptChangesDuringUpdate = from._acceptChangesDuringUpdate;
            _acceptChangesDuringUpdateAfterInsert = from._acceptChangesDuringUpdateAfterInsert;
            _continueUpdateOnError = from._continueUpdateOnError;
            _returnProviderSpecificTypes = from._returnProviderSpecificTypes; // WebData 101795
            _acceptChangesDuringFill = from._acceptChangesDuringFill;
            _fillLoadOption = from._fillLoadOption;
            _missingMappingAction = from._missingMappingAction;
            _missingSchemaAction = from._missingSchemaAction;

            if ((null != from._tableMappings) && (0 < from.TableMappings.Count))
            {
                DataTableMappingCollection parameters = this.TableMappings;
                foreach (object parameter in from.TableMappings)
                {
                    _ = parameters.Add((parameter is ICloneable) ? ((ICloneable)parameter).Clone() : parameter);
                }
            }
        }

        protected virtual DataTableMappingCollection CreateTableMappings()
        { // V1.0.3300
            return new DataTableMappingCollection();
        }

        protected override void Dispose(bool disposing)
        { // V1.0.3300, MDAC 65459
            if (disposing)
            { // release mananged objects
                _tableMappings = null;
            }
            // release unmanaged objects

            base.Dispose(disposing); // notify base classes
        }

        #region FillSchemaAsync

        public abstract Task<DataTable[]> FillSchemaAsync(DataSet dataSet, SchemaType schemaType);

        protected virtual async Task<DataTable[]> FillSchemaAsync(DataSet dataSet, SchemaType schemaType, string srcTable, DbDataReader dataReader)
        {
            if (null == dataSet) throw new ArgumentNullException(nameof(dataSet));
            if ((SchemaType.Source != schemaType) && (SchemaType.Mapped != schemaType)) throw ADP.InvalidSchemaType(schemaType);
            if (string.IsNullOrEmpty(srcTable)) throw ADP.FillSchemaRequiresSourceTableName("srcTable");
            if ((null == dataReader) || dataReader.IsClosed) throw ADP.FillRequires("dataReader");

            // user must Close/Dispose of the dataReader
            return await FillSchemaFromReaderAsync(dataSet, null, schemaType, srcTable, dataReader).ConfigureAwait(false);
        }

        protected virtual async Task<DataTable> FillSchemaAsync(DataTable dataTable, SchemaType schemaType, DbDataReader dataReader)
        {
            if (null == dataTable) throw new ArgumentNullException(nameof(dataTable));
            if ((SchemaType.Source != schemaType) && (SchemaType.Mapped != schemaType)) throw ADP.InvalidSchemaType(schemaType);
            if ((null == dataReader) || dataReader.IsClosed)throw ADP.FillRequires("dataReader");

            // user must Close/Dispose of the dataReader
            // user will have to call NextResult to access remaining results
            DataTable[] singleTable = await this.FillSchemaFromReaderAsync(null, dataTable, schemaType, null, dataReader).ConfigureAwait(false);
            if( singleTable != null ) return singleTable[0];
            return null;
        }

        internal async Task<DataTable[]> FillSchemaFromReaderAsync(DataSet dataset, DataTable singleDataTable, SchemaType schemaType, string srcTable, DbDataReader dataReader)
        {
            DataTable[] dataTables = null;
            int schemaCount = 0;
            do
            {
                AdaDataReaderContainer readerHandler = AdaDataReaderContainer.Create(dataReader, ReturnProviderSpecificTypes);

                AssertReaderHandleFieldCount(readerHandler);
                if (0 >= readerHandler.FieldCount)
                {
                    continue;
                }
                string tmp = null;
                if (null != dataset)
                {
                    tmp = AdaDataAdapter.GetSourceTableName(srcTable, schemaCount);
                    schemaCount++; // don't increment if no SchemaTable ( a non-row returning result )
                }

                AdaSchemaMapping mapping = new AdaSchemaMapping(this, dataset, singleDataTable, readerHandler, true, schemaType, tmp, false, null, null);

                if (singleDataTable != null)
                {
                    // do not read remaining results in single DataTable case
                    return new DataTable[] { mapping.DataTable };
                }
                else if (null != mapping.DataTable)
                {
                    if (null == dataTables)
                    {
                        dataTables = new DataTable[1] { mapping.DataTable };
                    }
                    else
                    {
                        dataTables = AdaDataAdapter.AddDataTableToArray(dataTables, mapping.DataTable);
                    }
                }
            }
            while (await dataReader.NextResultAsync().ConfigureAwait(false)); // FillSchema does not capture errors for FillError event

            if( dataTables is null && singleDataTable is null )
            {
                return Array.Empty<DataTable>();
            }
            else
            {
                return dataTables;
            }
        }

        #endregion

        public abstract Task<int> FillAsync( DataSet dataSet, CancellationToken cancellationToken );

        virtual protected async Task<int> FillAsync( DataSet dataSet, string srcTable, IDataReader dataReader, int startRecord, int maxRecords, CancellationToken cancellationToken )
        {
            if (null == dataSet)
            {
                throw ADP.FillRequires("dataSet");
            }
            if (string.IsNullOrEmpty(srcTable))
            {
                throw ADP.FillRequiresSourceTableName("srcTable");
            }
            if (null == dataReader)
            {
                throw ADP.FillRequires("dataReader");
            }
            if (startRecord < 0)
            {
                throw ADP.InvalidStartRecord("startRecord", startRecord);
            }
            if (maxRecords < 0)
            {
                throw ADP.InvalidMaxRecords("maxRecords", maxRecords);
            }
            if (dataReader.IsClosed)
            {
                return 0;
            }

            // user must Close/Dispose of the dataReader
            AdaDataReaderContainer readerHandler = AdaDataReaderContainer.Create(dataReader, ReturnProviderSpecificTypes);
            return await this.FillFromReaderAsync( dataSet, null, srcTable, readerHandler, startRecord, maxRecords, null, null, cancellationToken ).ConfigureAwait(false);
        }

        protected virtual async Task<int> FillAsync(DataTable dataTable, IDataReader dataReader, CancellationToken cancellationToken)
        {
            DataTable[] dataTables = new DataTable[] { dataTable };

            return await this.FillAsync( dataTables, dataReader, startRecord: 0, maxRecords: 0, cancellationToken ).ConfigureAwait(false);
        }

        protected virtual async Task<int> FillAsync( DataTable[] dataTables, IDataReader dataReader, int startRecord, int maxRecords, CancellationToken cancellationToken )
        {
            if (dataTables is null) throw new ArgumentNullException(paramName: nameof(dataTables));
            if (0 == dataTables.Length) throw new ArgumentException(string.Format("Argument is empty: {0}", nameof(dataTables)), paramName: nameof(dataTables));

            {
                if (null == dataTables[0])
                {
                    throw ADP.FillRequires("dataTable");
                }
                if (null == dataReader)
                {
                    throw ADP.FillRequires("dataReader");
                }
                if ((1 < dataTables.Length) && ((0 != startRecord) || (0 != maxRecords)))
                {
                    throw new NotSupportedException(); // FillChildren is not supported with FillPage
                }

                int result = 0;
                bool enforceContraints = false;
                DataSet commonDataSet = dataTables[0].DataSet;
                try
                {
                    if (null != commonDataSet)
                    {
                        enforceContraints = commonDataSet.EnforceConstraints;
                        commonDataSet.EnforceConstraints = false;
                    }
                    for (int i = 0; i < dataTables.Length; ++i)
                    {
                        Debug.Assert(null != dataTables[i], "null DataTable Fill");

                        if (dataReader.IsClosed)
                        {
#if DEBUG
                            Debug.Assert(!_debugHookNonEmptySelectCommand, "Debug hook asserts data reader should be open");
#endif
                            break;
                        }

                        AdaDataReaderContainer readerHandler = AdaDataReaderContainer.Create(dataReader, ReturnProviderSpecificTypes);
                        AssertReaderHandleFieldCount(readerHandler);
                       
                        if (readerHandler.FieldCount <= 0)
                        {
                            if (i == 0)
                            {
                                bool lastFillNextResult;
                                do
                                {
                                    lastFillNextResult = await this.FillNextResultAsync( readerHandler, cancellationToken ).ConfigureAwait(false);
                                }
                                while (lastFillNextResult && readerHandler.FieldCount <= 0);
                                
                                if (!lastFillNextResult)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }
                       
                        if ((0 < i) && !await this.FillNextResultAsync( readerHandler, cancellationToken ).ConfigureAwait(false))
                        {
                            break;
                        }
                        // user must Close/Dispose of the dataReader
                        // user will have to call NextResult to access remaining results
                        int count = await this.FillFromReaderAsync( null, dataTables[i], null, readerHandler, startRecord, maxRecords, null, null, cancellationToken ).ConfigureAwait(false);
                        if (0 == i)
                        {
                            result = count;
                        }
                    }
                }
                catch (ConstraintException)
                {
                    enforceContraints = false;
                    throw;
                }
                finally
                {
                    if (enforceContraints)
                    {
                        commonDataSet.EnforceConstraints = true;
                    }
                }

                return result;
            }
        }

        internal async Task<int> FillFromReaderAsync( DataSet dataset, DataTable datatable, string srcTable, AdaDataReaderContainer dataReader, int startRecord, int maxRecords, DataColumn parentChapterColumn, object parentChapterValue, CancellationToken cancellationToken )
        {
            int rowsAddedToDataSet = 0;
            int schemaCount = 0;
            do
            {
                AssertReaderHandleFieldCount(dataReader);
                if (0 >= dataReader.FieldCount)
                {
                    continue; // loop to next result
                }

                AdaSchemaMapping mapping = this.FillMapping( dataset, datatable, srcTable, dataReader, schemaCount, parentChapterColumn, parentChapterValue );
                schemaCount++; // don't increment if no SchemaTable ( a non-row returning result )

                AssertSchemaMapping(mapping);

                if (null == mapping)
                {
                    continue; // loop to next result
                }
                if (null == mapping.DataValues)
                {
                    continue; // loop to next result
                }
                if (null == mapping.DataTable)
                {
                    continue; // loop to next result
                }

                mapping.DataTable.BeginLoadData();

                try
                {
                    // startRecord and maxRecords only apply to the first resultset
                    if ((1 == schemaCount) && ((0 < startRecord) || (0 < maxRecords)))
                    {
                        rowsAddedToDataSet = await this.FillLoadDataRowChunkAsync( mapping, startRecord, maxRecords, cancellationToken ).ConfigureAwait(false);
                    }
                    else
                    {
                        int count = await this.FillLoadDataRowAsync( mapping, cancellationToken ).ConfigureAwait(false);

                        if (1 == schemaCount)
                        { // MDAC 71347
                            // only return LoadDataRow count for first resultset
                            // not secondary or chaptered results
                            rowsAddedToDataSet = count;
                        }
                    }
                }
                finally
                {
                    mapping.DataTable.EndLoadData();
                }
                if (null != datatable)
                {
                    break; // do not read remaining results in single DataTable case
                }
            }
            while (await this.FillNextResultAsync( dataReader, cancellationToken ).ConfigureAwait(false));

            return rowsAddedToDataSet;
        }

        private async Task<int> FillLoadDataRowChunkAsync( AdaSchemaMapping mapping, int startRecord, int maxRecords, CancellationToken cancellationToken )
        {
            AdaDataReaderContainer dataReader = mapping.DataReader;

            while (0 < startRecord)
            {
                if (!await dataReader.ReadAsync( cancellationToken ).ConfigureAwait(false))
                {
                    // there are no more rows on first resultset
                    return 0;
                }
                --startRecord;
            }

            int rowsAddedToDataSet = 0;
            if (0 < maxRecords)
            {
                while ((rowsAddedToDataSet < maxRecords) && await dataReader.ReadAsync( cancellationToken ).ConfigureAwait(false))
                {
                    if (_hasFillErrorHandler)
                    {
                        try
                        {
                            await mapping.LoadDataRowWithClearAsync( cancellationToken ).ConfigureAwait(false);
                            rowsAddedToDataSet++;
                        }
                        catch (Exception e)
                        {
                            // 
                            if (!ADP.IsCatchableExceptionType(e))
                            {
                                throw;
                            }
                            OnFillErrorHandler(e, mapping.DataTable, mapping.DataValues);
                        }
                    }
                    else
                    {
                        await mapping.LoadDataRowAsync( cancellationToken ).ConfigureAwait(false);
                        rowsAddedToDataSet++;
                    }
                }
                // skip remaining rows of the first resultset
            }
            else
            {
                rowsAddedToDataSet = await FillLoadDataRowAsync( mapping, cancellationToken ).ConfigureAwait(false);
            }
            return rowsAddedToDataSet;
        }

        private async Task<int> FillLoadDataRowAsync( AdaSchemaMapping mapping, CancellationToken cancellationToken )
        {
            int rowsAddedToDataSet = 0;
            AdaDataReaderContainer dataReader = mapping.DataReader;
            if (_hasFillErrorHandler)
            {
                while (await dataReader.ReadAsync( cancellationToken ).ConfigureAwait(false))
                { // read remaining rows of first and subsequent resultsets
                    try
                    {
                        // only try-catch if a FillErrorEventHandler is registered so that
                        // in the default case we get the full callstack from users
                        await mapping.LoadDataRowWithClearAsync( cancellationToken ).ConfigureAwait(false);
                        rowsAddedToDataSet++;
                    }
                    catch (Exception e)
                    {
                        // 
                        if (!ADP.IsCatchableExceptionType(e))
                        {
                            throw;
                        }
                        this.OnFillErrorHandler(e, mapping.DataTable, mapping.DataValues);
                    }
                }
            }
            else
            {
                while (await dataReader.ReadAsync( cancellationToken ).ConfigureAwait(false))
                {
                    // read remaining rows of first and subsequent resultset
                    await mapping.LoadDataRowAsync( cancellationToken ).ConfigureAwait(false);
                    rowsAddedToDataSet++;
                }
            }
            return rowsAddedToDataSet;
        }

        private AdaSchemaMapping FillMappingInternal(DataSet dataset, DataTable datatable, string srcTable, AdaDataReaderContainer dataReader, int schemaCount, DataColumn parentChapterColumn, object parentChapterValue)
        {
            bool withKeyInfo = (MissingSchemaAction.AddWithKey == this.MissingSchemaAction);
            string tmp = null;
            if (dataset != null)
            {
                tmp = AdaDataAdapter.GetSourceTableName(srcTable, schemaCount);
            }

            return new AdaSchemaMapping( this, dataset, datatable, dataReader, withKeyInfo, SchemaType.Mapped, tmp, true, parentChapterColumn, parentChapterValue );
        }

        private AdaSchemaMapping FillMapping(DataSet dataset, DataTable datatable, string srcTable, AdaDataReaderContainer dataReader, int schemaCount, DataColumn parentChapterColumn, object parentChapterValue)
        {
            AdaSchemaMapping mapping = null;
            if (_hasFillErrorHandler)
            {
                try
                {
                    // only try-catch if a FillErrorEventHandler is registered so that
                    // in the default case we get the full callstack from users
                    mapping = this.FillMappingInternal( dataset, datatable, srcTable, dataReader, schemaCount, parentChapterColumn, parentChapterValue );
                }
                catch (Exception e)
                {
                    if (!ADP.IsCatchableExceptionType(e))
                    {
                        throw;
                    }
                    this.OnFillErrorHandler(e, null, null);
                }
            }
            else
            {
                mapping = this.FillMappingInternal( dataset, datatable, srcTable, dataReader, schemaCount, parentChapterColumn, parentChapterValue );
            }
            return mapping;
        }

        private async Task<bool> FillNextResultAsync(AdaDataReaderContainer dataReader, CancellationToken cancellationToken )
        {
            bool result = true;
            if (_hasFillErrorHandler)
            {
                try
                {
                    // only try-catch if a FillErrorEventHandler is registered so that
                    // in the default case we get the full callstack from users
                    result = await dataReader.NextResultAsync( cancellationToken ).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (!ADP.IsCatchableExceptionType(e))
                    {
                        throw;
                    }
                    this.OnFillErrorHandler(e, null, null);
                }
            }
            else
            {
                result = await dataReader.NextResultAsync( cancellationToken ).ConfigureAwait(false);
            }

            return result;
        }

        [EditorBrowsableAttribute(EditorBrowsableState.Advanced)] // MDAC 69508
        virtual public IDataParameter[] GetFillParameters()
        { // V1.0.3300
            return new IDataParameter[0];
        }

        internal DataTableMapping GetTableMappingBySchemaAction(string sourceTableName, string dataSetTableName, MissingMappingAction mappingAction)
        {
            return DataTableMappingCollection.GetTableMappingBySchemaAction(_tableMappings, sourceTableName, dataSetTableName, mappingAction);
        }

        internal int IndexOfDataSetTable(string dataSetTable)
        {
            if (null != _tableMappings)
            {
                return TableMappings.IndexOfDataSetTable(dataSetTable);
            }
            return -1;
        }

        virtual protected void OnFillError(FillErrorEventArgs value)
        { // V1.2.3300, DbDataAdapter V1.0.3300
            FillErrorEventHandler handler = (FillErrorEventHandler)Events[EventFillError];
            if (null != handler)
            {
                handler(this, value);
            }
        }

        private void OnFillErrorHandler(Exception e, DataTable dataTable, object[] dataValues)
        {
            FillErrorEventArgs fillErrorEvent = new FillErrorEventArgs(dataTable, dataValues);
            fillErrorEvent.Errors = e;
            OnFillError(fillErrorEvent);

            if (!fillErrorEvent.Continue)
            {
                if (null != fillErrorEvent.Errors)
                {
                    throw fillErrorEvent.Errors;
                }
                throw e;
            }
        }

        virtual public Task<int> UpdateAsync(DataSet dataSet)
        { // V1.0.3300
            throw new NotSupportedException();
        }

        // used by FillSchema which returns an array of datatables added to the dataset
        static private DataTable[] AddDataTableToArray(DataTable[] tables, DataTable newTable)
        {
            for (int i = 0; i < tables.Length; ++i)
            { // search for duplicates
                if (tables[i] == newTable)
                {
                    return tables; // duplicate found
                }
            }
            DataTable[] newTables = new DataTable[tables.Length + 1]; // add unique data table
            for (int i = 0; i < tables.Length; ++i)
            {
                newTables[i] = tables[i];
            }
            newTables[tables.Length] = newTable;
            return newTables;
        }

        // dynamically generate source table names
        static private string GetSourceTableName(string srcTable, int index)
        {
            //if ((null != srcTable) && (0 <= index) && (index < srcTable.Length)) {
            if (0 == index)
            {
                return srcTable; //[index];
            }
            return srcTable + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    internal sealed class AdaLoadAdapter : AdaDataAdapter
    {
        internal AdaLoadAdapter()
        {
        }

        internal async Task<int> FillFromReaderAsync( DataTable[] dataTables, IDataReader dataReader, int startRecord, int maxRecords, CancellationToken cancellationToken )
        {
            return await this.FillAsync( dataTables, dataReader, startRecord, maxRecords, cancellationToken ).ConfigureAwait(false);
        }
    }
}
