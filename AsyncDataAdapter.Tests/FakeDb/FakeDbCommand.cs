using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncDataAdapter.Tests.FakeDb
{
    public class FakeDbCommand : DbCommand
    {
        /// <summary>NOTE: When using this constructor, ensure the <see cref="DbCommand.Connection"/> property is set before <see cref="DbDataAdapter.Fill(DataSet)"/> (or other overloads) are called.</summary>
        [Obsolete( "(Not actually obsolete, this attribute is just to warn you to not use this ctor unless you really know you need to)" )]
        public FakeDbCommand()
        {
            this.CreateReader = this.CreateFakeDbDataReader;
            this.Delays       = FakeDb.FakeDbDelays.DefaultDelaysNone;
        }

        public FakeDbCommand( FakeDbConnection connection, List<TestTable> testTables, FakeDbDelays delays )
        {
            base.Connection   = connection ?? throw new ArgumentNullException(nameof(connection));
            this.TestTables   = testTables;
            this.Delays       = delays ?? throw new ArgumentNullException(nameof(delays));

            this.CreateReader = this.CreateFakeDbDataReader;
        }

        #region Overridden

        public    override String                CommandText           { get; set; } // Base is abstract.
        public    override Int32                 CommandTimeout        { get; set; } // Base is abstract.
        public    override CommandType           CommandType           { get; set; } // Base is abstract.
        protected override DbConnection          DbConnection          { get; set; } // Base is abstract. The public one is non-virtual and directly reads/writes the protected abstract property (i.e. this one).
        protected override DbTransaction         DbTransaction         { get; set; } // Base is abstract.
        public    override Boolean               DesignTimeVisible     { get; set; } // Base is abstract.
        public    override UpdateRowSource       UpdatedRowSource      { get; set; } // Base is abstract.
        
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();

        //

        public new FakeDbConnection Connection => (FakeDbConnection)base.Connection;

        #endregion

        #region Test Data

        /// <summary>Used to prepopulate any <see cref="FakeDbDataReader"/> that's created.</summary>
        
        public List<TestTable> TestTables   { get; set; }
        public FakeDbDelays    Delays       { get; set; }
        public AsyncMode       AsyncMode    { get; set; }

        private FakeDbDataReader CreateFakeDbDataReader( FakeDbCommand cmd )
        {
            FakeDbDataReader reader = new FakeDbDataReader( cmd: cmd );
            if( this.TestTables != null )
            {
                reader.ResetAndLoadTestData( this.TestTables );
            }

            return reader;
        }

        public Func<FakeDbCommand,DbDataReader> CreateReader { get; set; }

        #endregion

        #region Misc

        public override void Cancel()
        {
        }

        protected override DbParameter CreateDbParameter()
        {
            return new FakeDbParameter();
        }

        public override void Prepare()
        {
        }

        #endregion

        #region Execute

        public Func< FakeDbCommand, Int32  > NonQueryResultRowCountValue { get; set; }
        public Func< FakeDbCommand, Object > ScalarQueryResultValue      { get; set; }

        private Int32 GetNonQueryResultRowCount()
        {
            Func<FakeDbCommand,Int32> func = this.NonQueryResultRowCountValue;
            if( func is null )
            {
                const string msg = nameof(this.NonQueryResultRowCountValue) + " has not been set.";
                NUnit.Framework.Assert.Fail( message: msg );
                throw new InvalidOperationException( msg );
            }

            return func( this );
        }

        private Object GetScalarQueryResult()
        {
            Func<FakeDbCommand,Object> func = this.ScalarQueryResultValue;
            if( func is null )
            {
                const string msg = nameof(this.ScalarQueryResultValue) + " has not been set.";
                NUnit.Framework.Assert.Fail( message: msg );
                throw new InvalidOperationException( msg );
            }

            return func( this );
        }

        #region Synchronous

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if( this.AsyncMode.AllowOld() )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return this.CreateReader( this );
            }
            else
            {
                throw new NotSupportedException( "AllowSync is false." );
            }
        }

        public override Int32 ExecuteNonQuery()
        {
            if( this.AsyncMode.AllowOld() )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return this.GetNonQueryResultRowCount();
            }
            else
            {
                throw new NotSupportedException( "AllowSync is false." );
            }
        }

        public override Object ExecuteScalar()
        {
            if( this.AsyncMode.AllowOld() )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return this.GetScalarQueryResult();
            }
            else
            {
                throw new NotSupportedException( "AllowSync is false." );
            }
        }

        #endregion

        #region ExecuteAsync

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            if( this.AsyncMode.HasFlag( AsyncMode.AwaitAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    await Task.Delay( this.Delays.Execute.Value ).ConfigureAwait(false);
                }

                return this.CreateReader( this );
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.BlockAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return this.CreateReader( this );
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.BaseAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return await base.ExecuteDbDataReaderAsync( behavior, cancellationToken );
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.RunAsync ) )
            {
                await Task.Yield();

                if( this.Delays.Execute.HasValue )
                {
                    await Task.Delay( this.Delays.Execute.Value ).ConfigureAwait(false);
                }

                return await Task.Run( () => this.CreateReader( this ) );
            }
            else
            {
                throw new NotSupportedException( "AllowAsync is false." );
            }
        }

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            if( this.AsyncMode.HasFlag( AsyncMode.AwaitAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    await Task.Delay( this.Delays.Execute.Value ).ConfigureAwait(false);
                }

                return this.GetNonQueryResultRowCount();
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.BlockAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return this.GetNonQueryResultRowCount();
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.BaseAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return await base.ExecuteNonQueryAsync( cancellationToken );
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.RunAsync ) )
            {
                await Task.Yield();

                if( this.Delays.Execute.HasValue )
                {
                    await Task.Delay( this.Delays.Execute.Value ).ConfigureAwait(false);
                }

                return await Task.Run( () => this.GetNonQueryResultRowCount() );
            }
            else
            {
                throw new NotSupportedException( "AllowAsync is false." );
            }
        }

        public override async Task<Object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            if( this.AsyncMode.HasFlag( AsyncMode.AwaitAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    await Task.Delay( this.Delays.Execute.Value ).ConfigureAwait(false);
                }

                return this.GetScalarQueryResult();
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.BlockAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return this.GetScalarQueryResult();
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.BaseAsync ) )
            {
                if( this.Delays.Execute.HasValue )
                {
                    Thread.Sleep( this.Delays.Execute.Value );
                }

                return await base.ExecuteScalarAsync( cancellationToken );
            }
            else if( this.AsyncMode.HasFlag( AsyncMode.RunAsync ) )
            {
                await Task.Yield();

                if( this.Delays.Execute.HasValue )
                {
                    await Task.Delay( this.Delays.Execute.Value ).ConfigureAwait(false);
                }

                return await Task.Run( () => this.GetScalarQueryResult() );
            }
            else
            {
                throw new NotSupportedException( "AllowAsync is false." );
            }
        }

        #endregion

        #endregion
    }
}
