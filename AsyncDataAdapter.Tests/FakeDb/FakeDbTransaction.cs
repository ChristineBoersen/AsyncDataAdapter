using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncDataAdapter.Tests.FakeDb
{
    public class FakeDbTransaction : DbTransaction
    {
        public FakeDbTransaction( FakeDbConnection c, IsolationLevel level )
            : base()
        {
            this.DbConnection   = c;
            this.IsolationLevel = level;
        }

        protected override DbConnection DbConnection { get; }

        public override IsolationLevel IsolationLevel { get; }

        public override void Commit()
        {
        }

#if NET6_0_OR_GREATER
        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
#endif
        public override void Rollback()
        {
        }


    }
}
