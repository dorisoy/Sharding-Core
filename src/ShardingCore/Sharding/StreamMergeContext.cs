using Microsoft.EntityFrameworkCore;
using ShardingCore.Core;
using ShardingCore.Core.Internal.Visitors.Selects;
using ShardingCore.Core.ShardingConfigurations.Abstractions;
using ShardingCore.Core.TrackerManagers;
using ShardingCore.Core.VirtualRoutes.DataSourceRoutes.RouteRuleEngine;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RouteTails.Abstractions;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RoutingRuleEngine;
using ShardingCore.Exceptions;
using ShardingCore.Extensions;
using ShardingCore.Extensions.InternalExtensions;
using ShardingCore.Sharding.Abstractions;
using ShardingCore.Sharding.MergeContexts;
using ShardingCore.Sharding.ShardingComparision.Abstractions;
using ShardingCore.Sharding.ShardingExecutors.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShardingCore.Core.RuntimeContexts;
using ShardingCore.Core.ShardingConfigurations;
using ShardingCore.Core.VirtualRoutes;
using ShardingCore.Sharding.MergeEngines.Abstractions;
using ShardingCore.Sharding.MergeEngines.Common.Abstractions;


namespace ShardingCore.Sharding
{
    /*
    * @Author: xjm
    * @Description:
    * @Date: Monday, 25 January 2021 11:38:27
    * @Email: 326308290@qq.com
    */
    public class StreamMergeContext : IMergeParseContext, IDisposable, IPrint
#if !EFCORE2
        , IAsyncDisposable
#endif
    {
        public IMergeQueryCompilerContext MergeQueryCompilerContext { get; }
        public IShardingRuntimeContext ShardingRuntimeContext { get; }
        public IParseResult ParseResult { get; }
        public IQueryable RewriteQueryable { get; }
        public IOptimizeResult OptimizeResult { get; }

        private readonly IRewriteResult _rewriteResult;
        private readonly IRouteTailFactory _routeTailFactory;

        public int? Skip { get; private set; }
        public int? Take { get; private set; }
        public PropertyOrder[] Orders { get; private set; }

        public SelectContext SelectContext => ParseResult.GetSelectContext();
        public GroupByContext GroupByContext => ParseResult.GetGroupByContext();
        public ShardingRouteResult ShardingRouteResult => MergeQueryCompilerContext.GetShardingRouteResult();

        /// <summary>
        /// ???????????????????????????
        /// </summary>
        public ISet<Type> QueryEntities { get; }


        /// <summary>
        /// ??????????????????
        /// </summary>
        public bool IsCrossDataSource => MergeQueryCompilerContext.IsCrossDataSource();

        /// <summary>
        /// ??????????????????
        /// </summary>
        public bool IsCrossTable => MergeQueryCompilerContext.IsCrossTable();

        private readonly ITrackerManager _trackerManager;
        private readonly ShardingConfigOptions _shardingConfigOptions;

        private readonly ConcurrentDictionary<DbContext, object> _parallelDbContexts;

        public IComparer<string> ShardingTailComparer => OptimizeResult.ShardingTailComparer();

        /// <summary>
        /// ????????????????????????????????????
        /// </summary>
        public bool TailComparerNeedReverse => OptimizeResult.SameWithTailComparer();


        public StreamMergeContext(IMergeQueryCompilerContext mergeQueryCompilerContext, IParseResult parseResult,
            IRewriteResult rewriteResult, IOptimizeResult optimizeResult)
        {
            MergeQueryCompilerContext = mergeQueryCompilerContext;
            ParseResult = parseResult;
            RewriteQueryable = rewriteResult.GetRewriteQueryable();
            OptimizeResult = optimizeResult;
            _rewriteResult = rewriteResult;
            ShardingRuntimeContext = mergeQueryCompilerContext.GetShardingDbContext().GetShardingRuntimeContext();
            _routeTailFactory = ShardingRuntimeContext.GetRouteTailFactory();
            _trackerManager = ShardingRuntimeContext.GetTrackerManager();
            _shardingConfigOptions = ShardingRuntimeContext.GetShardingConfigOptions();
            QueryEntities = MergeQueryCompilerContext.GetQueryEntities().Keys.ToHashSet();
            _parallelDbContexts = new ConcurrentDictionary<DbContext, object>();
            Orders = parseResult.GetOrderByContext().PropertyOrders.ToArray();
            Skip = parseResult.GetPaginationContext().Skip;
            Take = parseResult.GetPaginationContext().Take;
        }

        public void ReSetOrders(PropertyOrder[] orders)
        {
            Orders = orders;
        }

        public void ReSetSkip(int? skip)
        {
            Skip = skip;
        }

        public void ReSetTake(int? take)
        {
            Take = take;
        }

        /// <summary>
        /// ???????????????dbcontext
        /// </summary>
        /// <param name="sqlRouteUnit">???????????????????????????</param>
        /// <param name="connectionMode"></param>
        /// <returns></returns>
        public DbContext CreateDbContext(ISqlRouteUnit sqlRouteUnit, ConnectionModeEnum connectionMode)
        {
            var routeTail = _routeTailFactory.Create(sqlRouteUnit.TableRouteResult);
            //??????????????????????????????????????????????????????????????????????????????dbcontext?????????????????????????????????dispose
            var parallelQuery = IsParallelQuery();
            var strategy = !parallelQuery
                ? CreateDbContextStrategyEnum.ShareConnection
                : CreateDbContextStrategyEnum.IndependentConnectionQuery;
            var dbContext = GetShardingDbContext().GetDbContext(sqlRouteUnit.DataSourceName, strategy, routeTail);
            if (parallelQuery && RealConnectionMode(connectionMode) == ConnectionModeEnum.MEMORY_STRICTLY)
            {
                _parallelDbContexts.TryAdd(dbContext, null);
            }

            return dbContext;
        }

        /// <summary>
        /// ?????????????????????????????????????????????????????????????????????????????????
        /// ??????????????????????????????????????????????????????dbcontext???????????????????????????????????????
        /// </summary>
        /// <param name="connectionMode"></param>
        /// <returns></returns>
        public ConnectionModeEnum RealConnectionMode(ConnectionModeEnum connectionMode)
        {
            if (IsParallelQuery())
            {
                return connectionMode;
            }
            else
            {
                return ConnectionModeEnum.MEMORY_STRICTLY;
            }
        }

        //public IRouteTail Create(TableRouteResult tableRouteResult)
        //{
        //    return _routeTailFactory.Create(tableRouteResult);
        //}

        public IQueryable GetReWriteQueryable()
        {
            return RewriteQueryable;
        }

        public IQueryable GetOriginalQueryable()
        {
            return MergeQueryCompilerContext.GetQueryCombineResult().GetCombineQueryable();
        }

        public int? GetPaginationReWriteTake()
        {
            if (Take.HasValue)
                return Skip.GetValueOrDefault() + Take.Value;
            return default;
        }
        //public bool HasSkipTake()
        //{
        //    return Skip.HasValue || Take.HasValue;
        //}

        /// <summary>
        /// ??????skip??????take??????0?????????????????????????????????
        /// </summary>
        /// <returns></returns>
        public bool IsPaginationQuery()
        {
            return Skip is > 0 || Take is > 0;
        }


        public bool HasGroupQuery()
        {
            return this.GroupByContext.GroupExpression != null;
        }

        public bool IsMergeQuery()
        {
            return IsCrossDataSource || IsCrossTable;
        }

        public bool IsSingleShardingEntityQuery()
        {
            return QueryEntities.Where(o => MergeQueryCompilerContext.GetEntityMetadataManager().IsSharding(o)).Take(2)
                .Count() == 1;
        }

        public Type GetSingleShardingEntityType()
        {
            return QueryEntities.Single(o => MergeQueryCompilerContext.GetEntityMetadataManager().IsSharding(o));
        }
        //public bool HasAggregateQuery()
        //{
        //    return this.SelectContext.HasAverage();
        //}

        public IShardingDbContext GetShardingDbContext()
        {
            return MergeQueryCompilerContext.GetShardingDbContext();
        }

        public int GetMaxQueryConnectionsLimit()
        {
            return OptimizeResult.GetMaxQueryConnectionsLimit();
        }

        public ConnectionModeEnum GetConnectionMode(int sqlCount)
        {
            return CalcConnectionMode(sqlCount);
        }

        private ConnectionModeEnum CalcConnectionMode(int sqlCount)
        {
            switch (OptimizeResult.GetConnectionMode())
            {
                case ConnectionModeEnum.MEMORY_STRICTLY:
                case ConnectionModeEnum.CONNECTION_STRICTLY: return OptimizeResult.GetConnectionMode();
                default:
                {
                    return GetMaxQueryConnectionsLimit() < sqlCount
                        ? ConnectionModeEnum.CONNECTION_STRICTLY
                        : ConnectionModeEnum.MEMORY_STRICTLY;
                    ;
                }
            }
        }

        /// <summary>
        /// ????????????????????????
        /// </summary>
        /// <returns></returns>
        private bool IsUseReadWriteSeparation()
        {
            return GetShardingDbContext().IsUseReadWriteSeparation() &&
                   GetShardingDbContext().CurrentIsReadWriteSeparation();
        }

        /// <summary>
        /// ??????????????????????????????????????????????????????????????????DbContext???????????????????????????????????????????????????DbContext??????????????????
        /// </summary>
        /// <returns></returns>
        public bool IsParallelQuery()
        {
            return MergeQueryCompilerContext.IsParallelQuery();
        }

        /// <summary>
        /// ????????????sharding track
        /// </summary>
        /// <returns></returns>
        public bool IsUseShardingTrack(Type entityType)
        {
            if (!IsParallelQuery())
                return false;
            return QueryTrack() && _trackerManager.EntityUseTrack(entityType);
        }

        private bool QueryTrack()
        {
            return MergeQueryCompilerContext.IsQueryTrack();
        }

        public IShardingComparer GetShardingComparer()
        {
            return GetShardingDbContext().GetShardingRuntimeContext().GetRequiredService<IShardingComparer>();
        }

        /// <summary>
        /// ????????????false???????????????????????????????????????
        /// ??????true????????????????????????
        /// </summary>
        /// <param name="emptyFunc"></param>
        /// <param name="r"></param>
        /// <typeparam name="TResult"></typeparam>
        /// <returns></returns>
        /// <exception cref="ShardingCoreQueryRouteNotMatchException"></exception>
        public bool TryPrepareExecuteContinueQuery<TResult>(Func<TResult> emptyFunc, out TResult r)
        {
            if (TakeZeroNoQueryExecute())
            {
                r = emptyFunc();
                return false;
            }

            if (IsRouteNotMatch())
            {
                if (ThrowIfQueryRouteNotMatch())
                {
                    throw new ShardingCoreQueryRouteNotMatchException(MergeQueryCompilerContext.GetQueryExpression()
                        .ShardingPrint());
                }
                else
                {
                    r = emptyFunc();
                    return false;
                }
            }

            r = default;
            return true;
        }

        /// <summary>
        /// ???????????????
        /// </summary>
        /// <returns></returns>
        public bool IsRouteNotMatch()
        {
            return ShardingRouteResult.IsEmpty;
        }

        /// <summary>
        /// take???????????????0??????????????????????????????????????????
        /// </summary>
        /// <returns></returns>
        public bool TakeZeroNoQueryExecute()
        {
            return Take is 0;
        }

        private bool ThrowIfQueryRouteNotMatch()
        {
            return _shardingConfigOptions.ThrowIfQueryRouteNotMatch;
        }

        public bool UseUnionAllMerge()
        {
            return MergeQueryCompilerContext.UseUnionAllMerge();
        }

        public void Dispose()
        {
            foreach (var dbContext in _parallelDbContexts.Keys)
            {
                dbContext.Dispose();
            }
        }
#if !EFCORE2

        public async ValueTask DisposeAsync()
        {
            foreach (var dbContext in _parallelDbContexts.Keys)
            {
                await dbContext.DisposeAsync();
            }
        }
#endif
        public bool IsSeqQuery()
        {
            return OptimizeResult.IsSequenceQuery();
        }

        public string GetPrintInfo()
        {
            return
                $"stream merge context:[max query connections limit:{GetMaxQueryConnectionsLimit()}],[is use read write separation:{IsUseReadWriteSeparation()}],[is parallel query:{IsParallelQuery()}],[is not support sharding:{UseUnionAllMerge()}],[is sequence query:{IsSeqQuery()}],[is route not match:{IsRouteNotMatch()}],[throw if query route not match:{ThrowIfQueryRouteNotMatch()}],[is pagination query:{IsPaginationQuery()}],[has group query:{HasGroupQuery()}],[is merge query:{IsMergeQuery()}],[is single sharding entity query:{IsSingleShardingEntityQuery()}]";
        }

        public int? GetSkip()
        {
            return Skip;
        }

        public int? GetTake()
        {
            return Take;
        }

        public void ReverseOrder()
        {
            if (Orders.Any())
            {
                var propertyOrders = Orders.Select(o => new PropertyOrder(o.PropertyExpression, !o.IsAsc, o.OwnerType))
                    .ToArray();
                ReSetOrders(propertyOrders);
            }
        }
    }
}