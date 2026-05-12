namespace DesktopToDo.Models
{
    /// <summary>
    /// 置顶任务的序号显示模式
    /// </summary>
    public enum PinnedTaskNumberMode
    {
        /// <summary>
        /// 统一排序：置顶任务与非置顶任务使用统一序号序列
        /// </summary>
        Unified,

        /// <summary>
        /// 单独排序：置顶任务和非置顶任务各自独立编号
        /// </summary>
        Separate
    }
}
