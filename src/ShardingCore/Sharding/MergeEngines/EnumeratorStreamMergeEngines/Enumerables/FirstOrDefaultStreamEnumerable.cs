using ShardingCore.Sharding.Enumerators;
using ShardingCore.Sharding.MergeEngines.Abstractions;
using ShardingCore.Sharding.MergeEngines.Abstractions.StreamMerge;
using ShardingCore.Sharding.MergeEngines.EnumeratorStreamMergeEngines.StreamMergeCombines;
using ShardingCore.Sharding.MergeEngines.Executors.Abstractions;
using ShardingCore.Sharding.MergeEngines.Executors.Enumerators;

namespace ShardingCore.Sharding.MergeEngines.EnumeratorStreamMergeEngines.Enumerables
{
    internal class FirstOrDefaultStreamMergeEnumerable<TEntity>:AbstractStreamEnumerable<TEntity>
    {
        public FirstOrDefaultStreamMergeEnumerable(StreamMergeContext streamMergeContext) : base(streamMergeContext)
        {
        }

        protected override IStreamMergeCombine GetStreamMergeCombine()
        {
            return DefaultStreamMergeCombine.Instance;
        }

        protected override IExecutor<IStreamMergeAsyncEnumerator<TEntity>> CreateExecutor0(bool async)
        {
            return new DefaultEnumeratorExecutor<TEntity>(GetStreamMergeContext(), GetStreamMergeCombine(), async);
        }
    }
}