using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShardingCore.Core.EntityMetadatas;
using ShardingCore.Core.QueryRouteManagers;
using ShardingCore.Core.QueryRouteManagers.Abstractions;
using ShardingCore.Core.ShardingPage;
using ShardingCore.Core.ShardingPage.Abstractions;
using ShardingCore.Core.TrackerManagers;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources;
using ShardingCore.Core.VirtualRoutes;
using ShardingCore.Core.VirtualRoutes.DataSourceRoutes.RouteRuleEngine;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RouteTails.Abstractions;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RoutingRuleEngine;
using ShardingCore.EFCores;
using ShardingCore.EFCores.OptionsExtensions;
using ShardingCore.Jobs;
using ShardingCore.Sharding;
using ShardingCore.Sharding.Abstractions;
using ShardingCore.Sharding.ShardingQueryExecutors;
using ShardingCore.TableCreator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Migrations;
using ShardingCore.Bootstrappers;
using ShardingCore.Core.DbContextCreator;
using ShardingCore.Core.QueryTrackers;
using ShardingCore.Core.RuntimeContexts;
using ShardingCore.Core.ShardingConfigurations.ConfigBuilders;
using ShardingCore.Core.ShardingMigrations;
using ShardingCore.Core.ShardingMigrations.Abstractions;
using ShardingCore.Core.UnionAllMergeShardingProviders;
using ShardingCore.Core.UnionAllMergeShardingProviders.Abstractions;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources.Abstractions;
using ShardingCore.Core.VirtualRoutes.Abstractions;
using ShardingCore.Core.VirtualRoutes.DataSourceRoutes;
using ShardingCore.Core.VirtualRoutes.TableRoutes;
using ShardingCore.DynamicDataSources;
using ShardingCore.Exceptions;
using ShardingCore.Extensions;
using ShardingCore.Sharding.MergeContexts;
using ShardingCore.Sharding.ParallelTables;
using ShardingCore.Sharding.Parsers;
using ShardingCore.Sharding.Parsers.Abstractions;
using ShardingCore.Sharding.ReadWriteConfigurations;
using ShardingCore.Sharding.ReadWriteConfigurations.Abstractions;
using ShardingCore.Sharding.ShardingComparision;
using ShardingCore.Sharding.ShardingComparision.Abstractions;
using ShardingCore.Sharding.ShardingExecutors;
using ShardingCore.Sharding.ShardingExecutors.NativeTrackQueries;
using ShardingCore.TableExists;
using ShardingCore.TableExists.Abstractions;

namespace ShardingCore
{
    /*
    * @Author: xjm
    * @Description:
    * @Date: Thursday, 28 January 2021 13:32:18
    * @Email: 326308290@qq.com
    */
    public static class ShardingCoreExtension
    {
        /// <summary>
        /// ??????ShardingCore?????????EntityFrameworkCore???<![CDATA[services.AddDbContext<TShardingDbContext>]]>
        /// </summary>
        /// <typeparam name="TShardingDbContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="contextLifetime"></param>
        /// <param name="optionsLifetime"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static ShardingCoreConfigBuilder<TShardingDbContext> AddShardingDbContext<TShardingDbContext>(
            this IServiceCollection services,
            ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
            ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
            where TShardingDbContext : DbContext, IShardingDbContext
        {
            if (contextLifetime == ServiceLifetime.Singleton)
                throw new NotSupportedException($"{nameof(contextLifetime)}:{nameof(ServiceLifetime.Singleton)}");
            if (optionsLifetime == ServiceLifetime.Singleton)
                throw new NotSupportedException($"{nameof(optionsLifetime)}:{nameof(ServiceLifetime.Singleton)}");
            services.AddDbContext<TShardingDbContext>(UseDefaultSharding<TShardingDbContext>, contextLifetime,
                optionsLifetime);
            return services.AddShardingConfigure<TShardingDbContext>();
        }

        public static ShardingCoreConfigBuilder<TShardingDbContext> AddShardingConfigure<TShardingDbContext>(
            this IServiceCollection services)
            where TShardingDbContext : DbContext, IShardingDbContext
        {
            //ShardingCoreHelper.CheckContextConstructors<TShardingDbContext>();
            return new ShardingCoreConfigBuilder<TShardingDbContext>(services);
        }

        public static void UseDefaultSharding<TShardingDbContext>(IServiceProvider serviceProvider,
            DbContextOptionsBuilder dbContextOptionsBuilder) where TShardingDbContext : DbContext, IShardingDbContext
        {
            var shardingRuntimeContext = serviceProvider.GetRequiredService<IShardingRuntimeContext>();
            dbContextOptionsBuilder.UseDefaultSharding<TShardingDbContext>(shardingRuntimeContext);
        }
        public static void UseDefaultSharding<TShardingDbContext>(this DbContextOptionsBuilder dbContextOptionsBuilder,IShardingRuntimeContext shardingRuntimeContext) where TShardingDbContext : DbContext, IShardingDbContext
        {
            var shardingConfigOptions = shardingRuntimeContext.GetShardingConfigOptions();
            shardingConfigOptions.ShardingMigrationConfigure?.Invoke(dbContextOptionsBuilder);
            var virtualDataSource = shardingRuntimeContext.GetVirtualDataSource();
            var connectionString = virtualDataSource.GetConnectionString(virtualDataSource.DefaultDataSourceName);
            var contextOptionsBuilder = virtualDataSource.ConfigurationParams
                .UseDbContextOptionsBuilder(connectionString, dbContextOptionsBuilder)
                .UseShardingMigrator()
                .UseSharding<TShardingDbContext>(shardingRuntimeContext);

            virtualDataSource.ConfigurationParams.UseShellDbContextOptionBuilder(contextOptionsBuilder);
        }

        internal static IServiceCollection AddInternalShardingCore<TShardingDbContext>(this IServiceCollection services)
            where TShardingDbContext : DbContext, IShardingDbContext
        {
            services.TryAddSingleton<IShardingInitializer, ShardingInitializer>();
            services.TryAddSingleton<IShardingBootstrapper, ShardingBootstrapper>();
            services.TryAddSingleton<IDataSourceInitializer, DataSourceInitializer>();
            services.TryAddSingleton<ITableRouteManager, TableRouteManager>();
            services
                .TryAddSingleton<IVirtualDataSourceConfigurationParams, SimpleVirtualDataSourceConfigurationParams>();
            //??????dbcontext??????
            services.TryAddSingleton<IDbContextCreator, ActivatorDbContextCreator<TShardingDbContext>>();


            // services.TryAddSingleton<IDataSourceInitializer<TShardingDbContext>, DataSourceInitializer<TShardingDbContext>>();
            services.TryAddSingleton<ITrackerManager, TrackerManager>();
            services.TryAddSingleton<IStreamMergeContextFactory, StreamMergeContextFactory>();
            services.TryAddSingleton<IShardingTableCreator, ShardingTableCreator>();
            //?????????????????????
            services.TryAddSingleton<IVirtualDataSource, VirtualDataSource>();
            services.TryAddSingleton<IDataSourceRouteManager, DataSourceRouteManager>();
            services.TryAddSingleton<IDataSourceRouteRuleEngine, DataSourceRouteRuleEngine>();
            services.TryAddSingleton<IDataSourceRouteRuleEngineFactory, DataSourceRouteRuleEngineFactory>();
            //??????????????????????????????
            services.TryAddSingleton<IShardingReadWriteAccessor, ShardingReadWriteAccessor>();
            services.TryAddSingleton<IReadWriteConnectorFactory, ReadWriteConnectorFactory>();

            //???????????????
            // services.TryAddSingleton<IVirtualTableManager<TShardingDbContext>, VirtualTableManager<TShardingDbContext>>();
            //?????????????????????????????????
            services.TryAddSingleton<IEntityMetadataManager, DefaultEntityMetadataManager>();

            //????????????
            services.TryAddSingleton<ITableRouteRuleEngineFactory, TableRouteRuleEngineFactory>();
            services.TryAddSingleton<ITableRouteRuleEngine, TableRouteRuleEngine>();
            //??????????????????
            services.TryAddSingleton<IParallelTableManager, ParallelTableManager>();
            services.TryAddSingleton<IRouteTailFactory, RouteTailFactory>();
            services.TryAddSingleton<IShardingCompilerExecutor, DefaultShardingCompilerExecutor>();
            services.TryAddSingleton<IQueryCompilerContextFactory, QueryCompilerContextFactory>();
            services.TryAddSingleton<IShardingQueryExecutor, DefaultShardingQueryExecutor>();

            //
            services.TryAddSingleton<IPrepareParser, DefaultPrepareParser>();
            services.TryAddSingleton<IQueryableParseEngine, QueryableParseEngine>();
            services.TryAddSingleton<IQueryableRewriteEngine, QueryableRewriteEngine>();
            services.TryAddSingleton<IQueryableOptimizeEngine, QueryableOptimizeEngine>();

            //migration manage
            services.TryAddSingleton<IShardingMigrationAccessor, ShardingMigrationAccessor>();
            services.TryAddSingleton<IShardingMigrationManager, ShardingMigrationManager>();

            //route manage
            services.TryAddSingleton<IShardingRouteManager, ShardingRouteManager>();
            services.TryAddSingleton<IShardingRouteAccessor, ShardingRouteAccessor>();

            //sharding page
            services.TryAddSingleton<IShardingPageManager, ShardingPageManager>();
            services.TryAddSingleton<IShardingPageAccessor, ShardingPageAccessor>();

            services.TryAddSingleton<IShardingBootstrapper, ShardingBootstrapper>();
            services.TryAddSingleton<IUnionAllMergeManager, UnionAllMergeManager>();
            services.TryAddSingleton<IUnionAllMergeAccessor, UnionAllMergeAccessor>();
            services.TryAddSingleton<IQueryTracker, QueryTracker>();
            services.TryAddSingleton<IShardingTrackQueryExecutor, DefaultShardingTrackQueryExecutor>();
            services.TryAddSingleton<INativeTrackQueryExecutor, NativeTrackQueryExecutor>();
            //????????????????????????
            services.TryAddSingleton<IShardingReadWriteManager, ShardingReadWriteManager>();
            services.TryAddSingleton<IShardingComparer, CSharpLanguageShardingComparer>();

            services.TryAddSingleton<ITableEnsureManager, EmptyTableEnsureManager>();

            services.TryAddShardingJob();
            return services;
        }

        public static DbContextOptionsBuilder UseSharding<TShardingDbContext>(
            this DbContextOptionsBuilder optionsBuilder, IShardingRuntimeContext shardingRuntimeContext)
            where TShardingDbContext : DbContext, IShardingDbContext
        {
            return optionsBuilder.UseShardingWrapMark().UseShardingOptions(shardingRuntimeContext)
                .ReplaceService<IDbSetSource, ShardingDbSetSource>()
                .ReplaceService<IQueryCompiler, ShardingQueryCompiler>()
                .ReplaceService<IDbContextTransactionManager,
                    ShardingRelationalTransactionManager<TShardingDbContext>>()
                .ReplaceService<IRelationalTransactionFactory,
                    ShardingRelationalTransactionFactory<TShardingDbContext>>();
        }
        public static DbContextOptionsBuilder UseShardingMigrator(
            this DbContextOptionsBuilder optionsBuilder)
        {
            return optionsBuilder
                .ReplaceService<IMigrator, ShardingMigrator>();
        }

        public static DbContextOptionsBuilder UseShardingOptions(this DbContextOptionsBuilder optionsBuilder,
            IShardingRuntimeContext shardingRuntimeContext)
        {
            var shardingOptionsExtension = optionsBuilder.CreateOrGetShardingOptionsExtension(shardingRuntimeContext);
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(shardingOptionsExtension);
            return optionsBuilder;
        }


        private static DbContextOptionsBuilder UseShardingWrapMark(this DbContextOptionsBuilder optionsBuilder)
        {
            var shardingWrapExtension = optionsBuilder.CreateOrGetShardingWrapExtension();
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(shardingWrapExtension);
            return optionsBuilder;
        }

        private static ShardingWrapOptionsExtension CreateOrGetShardingWrapExtension(
            this DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.Options.FindExtension<ShardingWrapOptionsExtension>() ??
               new ShardingWrapOptionsExtension();

        private static ShardingOptionsExtension CreateOrGetShardingOptionsExtension(
            this DbContextOptionsBuilder optionsBuilder, IShardingRuntimeContext shardingRuntimeContext)
            => optionsBuilder.Options.FindExtension<ShardingOptionsExtension>() ??
               new ShardingOptionsExtension(shardingRuntimeContext);

        public static DbContextOptionsBuilder UseInnerDbContextSharding(this DbContextOptionsBuilder optionsBuilder)
        {
            return optionsBuilder.ReplaceService<IModelCacheKeyFactory, ShardingModelCacheKeyFactory>()
                .ReplaceService<IModelSource, ShardingModelSource>()
                .ReplaceService<IModelCustomizer, ShardingModelCustomizer>();
        }


        /// <summary>
        /// ?????????????????????????????????
        /// </summary>
        /// <param name="serviceProvider"></param>
        public static void UseAutoShardingCreate(this IServiceProvider serviceProvider)
        {
            var shardingRuntimeContext = serviceProvider.GetRequiredService<IShardingRuntimeContext>();
            shardingRuntimeContext.CheckRequirement();
            shardingRuntimeContext.AutoShardingCreate();
        }

        /// <summary>
        /// ?????????????????????
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="parallelCount"></param>
        public static void UseAutoTryCompensateTable(this IServiceProvider serviceProvider, int? parallelCount = null)
        {
            var shardingRuntimeContext = serviceProvider.GetRequiredService<IShardingRuntimeContext>();
            shardingRuntimeContext.UseAutoTryCompensateTable(parallelCount);
        }


        //public static IServiceCollection AddSingleShardingDbContext<TShardingDbContext>(this IServiceCollection services, Action<ShardingConfigOptions> configure,
        //    Action<string, DbContextOptionsBuilder> optionsAction = null,
        //    ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        //    ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        //    where TShardingDbContext : DbContext, IShardingDbContext
        //{
        //    if (contextLifetime == ServiceLifetime.Singleton)
        //        throw new NotSupportedException($"{nameof(contextLifetime)}:{nameof(ServiceLifetime.Singleton)}");
        //    if (optionsLifetime == ServiceLifetime.Singleton)
        //        throw new NotSupportedException($"{nameof(optionsLifetime)}:{nameof(ServiceLifetime.Singleton)}");
        //    Action<IServiceProvider, DbContextOptionsBuilder> shardingOptionAction = (sp, option) =>
        //    {
        //        var virtualDataSource = sp.GetRequiredService<IVirtualDataSourceManager<TShardingDbContext>>().GetCurrentVirtualDataSource();
        //        var connectionString = virtualDataSource.GetConnectionString(virtualDataSource.DefaultDataSourceName);
        //        optionsAction?.Invoke(connectionString, option);
        //        option.UseSharding<TShardingDbContext>();
        //    };
        //    services.AddDbContext<TShardingDbContext>(shardingOptionAction, contextLifetime, optionsLifetime);
        //    return services.AddShardingConfigure<TShardingDbContext>(optionsAction);
        //}
    }
}