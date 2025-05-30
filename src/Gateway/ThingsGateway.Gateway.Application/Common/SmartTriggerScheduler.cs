//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

namespace ThingsGateway.Gateway.Application;

public class SmartTriggerScheduler
{
    private readonly object _lock = new();          // 锁对象，保证线程安全
    private readonly Func<Task> _action;                // 实际要执行的操作
    private readonly TimeSpan _delay;               // 执行间隔（冷却时间）

    private bool _isRunning = false;                // 当前是否有调度任务在运行
    private bool _hasPending = false;               // 在等待期间是否有新的触发

    // 构造函数，传入要执行的方法和最小执行间隔
    public SmartTriggerScheduler(Func<Task> action, TimeSpan minimumInterval)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _delay = minimumInterval;
    }

    // 外部调用的触发方法（高频调用的地方调用这个）
    public void Trigger()
    {
        lock (_lock)
        {

            if (_isRunning)
            {
                // 如果正在执行中，标记为“等待处理”，之后再执行一次
                _hasPending = true;
                return;
            }

            // 否则启动执行任务
            _isRunning = true;
            _ = Task.Run(ExecuteLoop); // 开启异步执行循环（非阻塞）
        }
    }

    // 实际执行动作的循环逻辑
    private async Task ExecuteLoop()
    {
        while (true)
        {
            Func<Task> actionToRun = null;

            // 拷贝 _action，并清除等待标记
            lock (_lock)
            {
                _hasPending = false;       // 当前这一轮已经处理了触发
                actionToRun = _action;     // 拷贝要执行的逻辑（避免锁内执行）
            }

            // 执行外部提供的方法
            await actionToRun().ConfigureAwait(false);

            // 等待 delay 时间，进入冷却期
            await Task.Delay(_delay).ConfigureAwait(false);

            lock (_lock)
            {
                // 冷却期后检查是否在这段时间内有新的触发
                if (!_hasPending)
                {
                    // 没有新的触发了，结束执行循环
                    _isRunning = false;
                    return;
                }

            }
        }
    }
}
