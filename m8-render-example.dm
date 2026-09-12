// M8 代码弹幕(应援弹幕)渲染示例脚本
// 说明: 本条脚本用于验证 M8Win2DRenderHost 渲染管线。
// 把本文内容粘贴到 PlayerPage 的 M8 代码弹幕包(或按工单验收直接注入 RunM8Script)即可看到
// 一个带位移动画的文本元素叠加在弹幕层上方。语法为原 Flash 弹幕脚本子集。
var comment = Display.createComment("M8代码弹幕渲染示例", {
    x: 120,
    y: 220,
    lifeTime: 3,
    motion: {
        x: { fromValue: -50, toValue: 320 },
        alpha: { fromValue: 1, toValue: 1 }
    }
});
Global._set("rendered", comment);