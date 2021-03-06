using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ShardingCore.Jobs.Abstaractions
{
    public interface IJob
    {
        string JobName { get; }
        string[] GetCronExpressions();
        Task ExecuteAsync();
        bool AppendJob();
    }
}
