'use strict';

// $.root 与 NSDanmaku 的显示层代理：不依赖 UWP 或浏览器。
const assert = require('node:assert/strict');
const { loadHost } = require('./retained-mode.test');
const layer = '$.root.getChildByName("__biliuwpOrdinaryDanmaku")';
const take = `$.root.removeChild(${layer});`;
const item = (id, code, duration = 10) => ({id, code, duration, stime:0, lang:'js'});
const events = host => host.messages.filter(message => message.type === 'ordinaryDanmaku');
const suppressed = host => events(host).at(-1).suppressed;

async function create() {
    const host = await loadHost();
    host.reset(0, true, 1, true);
    host.api.setOrdinaryDanmakuBridge(7);
    return host;
}

async function run() {
    {
        const host = await create();
        host.api.appendComments([
            {txt:'blocked word',time:1,mode:1,blocked:true,blockType:2},
            {txt:'user',time:2,mode:1,blocked:true,blockType:1},
            {txt:'visible',time:3,mode:1}
        ]);
        host.append([item('snapshot', `window.__list=Player.commentList;window.__entry=Player.commentList[0];
            window.__visible=Player.commentList.filter(function(c){return !c.blocked;}).map(function(c){return c.txt;});
            Player.commentTrigger(function(c){window.__sent=c;},10000);`)]);
        host.runFrames(1);
        const list = host.sandbox.__list;
        const entry = host.sandbox.__entry;
        assert.equal(list.length, 3, '屏蔽只标记，不删除 M8 列表内容');
        assert.equal(entry.blocked, true);
        assert.equal(entry.blockType, 2);
        assert.equal(JSON.stringify(host.sandbox.__visible), '["visible"]');
        assert.equal(list[2].blocked, false);
        assert.equal(list[2].blockType, 0);

        host.api.beginComments();
        host.api.appendComments([{txt:'blocked word',time:1,mode:1,blocked:false,blockType:0}]);
        host.api.appendComments([
            {txt:'user',time:2,mode:1,blocked:false,blockType:0},
            {txt:'visible',time:3,mode:1,blocked:true,blockType:4}
        ]);
        host.api.finishComments();
        assert.equal(host.sandbox.__list, list, '规则刷新保留已取得的数组引用');
        assert.equal(list[0], entry, '规则刷新保留已取得的弹幕对象引用');
        assert.equal(entry.blocked, false, '移除规则后原对象解除屏蔽');
        assert.equal(entry.blockType, 0);
        assert.equal(list[2].blockType, 4);
        assert.equal(list[2].blocked, true);
        host.api.pushComment({txt:'sent',blocked:true,blockType:2});
        assert.equal(host.sandbox.__sent.blockType, 2);
        assert.equal(host.sandbox.__sent.blocked, true);
        assert.equal(list.length, 4);
        host.api.beginComments();
        host.api.finishComments();
        assert.equal(list.length, 0, '空快照会清理旧条目');
        host.api.resetComments();
        host.api.appendComments([{txt:'new video'}]);
        host.append([item('new-snapshot', 'window.__newList=Player.commentList;')]);
        host.runFrames(1);
        assert.notEqual(host.sandbox.__newList, list, '内容重置隔离旧视频的引用');
        assert.equal(host.sandbox.__newList[0].blocked, false);
        assert.equal(host.errors().length, 0);
        console.log('ok M8屏蔽标记 / 完整列表 / 规则刷新保持引用 / 发送链 / 内容隔离');
    }
    {
        const host = await create();
        host.append([item('alpha', `$.root.addEventListener('added',function(e){
            if(!e.target.selectable)return;
            window.__class=String(e.target.constructor);
            window.__enumerable=Object.keys(e.target).includes('constructor');
            e.target.alpha=e.target.data.txt==='transparent'?0:0.5;
        });`)]);
        host.runFrames(1);
        assert.equal(host.api.filterOrdinaryDanmaku({txt:'transparent',mode:1}), false);
        assert.equal(host.sandbox.__class, '[class ScrollComment]');
        assert.equal(host.sandbox.__enumerable, false, '类型信息不能进入脚本foreach');
        assert.equal(host.api.filterOrdinaryDanmaku({txt:'partial',mode:1}), true,
            '仅完全透明才阻止原生弹幕入屏');
        assert.equal(host.errors().length, 0);
        console.log('ok ScrollComment类名 / constructor不可枚举 / alpha归零筛选 / 半透明保留');
    }
    {
        const host = await create();
        host.append([item('decoration', `
            var canvas=$.createCanvas({lifeTime:10});
            var text=$.createComment('decoration',{parent:canvas,lifeTime:10});
            canvas.removeChild(text);$.root.removeChild(canvas);
            window.__root=$.root;`)]);
        host.runFrames(10);
        assert.equal(suppressed(host), false, '删除 M8 自己的元素不应屏蔽普通弹幕');
        assert.equal(events(host).length, 1);
        assert.ok(host.sandbox.__root.getChildByName('__biliuwpOrdinaryDanmaku'));
        console.log('ok 装饰 M8 与普通弹幕共存');
    }
    {
        const host = await create();
        host.api.appendComments([{txt:'bullet',time:1,color:0xffffff}]);
        host.append([item('game', `
            var canvas=$.createCanvas({lifeTime:10});window.__canvas=canvas;
            $.root.addEventListener('enterFrame',function(){
                $.root.setChildIndex(canvas,0);
                for(var i=$.root.numChildren-2;i>=1;i--) $.root.removeChildAt(i);
            });
            Player.keyTrigger(function(){ScriptManager.clearEl();},10000);
            window.__comments=Player.commentList;`)]);
        host.runFrames(60);
        assert.equal(suppressed(host), true, '原脚本保留首尾的清除循环必须删除普通弹幕代理');
        assert.equal(events(host).length, 2, '持续清除只发送一次接管通知');
        assert.equal(host.sandbox.__canvas.parent.getChildIndex(host.sandbox.__canvas), 0);
        assert.equal(host.sandbox.__comments[0].txt, 'bullet', '接管不删除弹幕池');
        host.setState(1, false, 1);
        assert.equal(suppressed(host), true, '暂停不释放接管');
        host.api.visible(false);
        assert.equal(suppressed(host), false);
        host.api.visible(true);
        assert.equal(suppressed(host), true, '重新显示恢复原脚本的接管');
        host.api.pushKey(65, false);
        assert.equal(suppressed(host), false, 'clearEl 立即恢复普通弹幕');
        assert.equal(host.errors().length, 0);
        console.log('ok 原游戏的显示树清除 / 暂停与隐藏 / 数据保留 / clearEl 恢复');
    }
    {
        const host = await create();
        host.append([
            item('first', `var layer=${layer};` + take + 'Player.keyTrigger(function(){$.root.addChild(layer);},10000);', 1),
            item('second', take, 2)
        ]);
        host.runFrames(1);
        assert.equal(suppressed(host), true);
        host.api.pushKey(65, false);
        assert.equal(suppressed(host), true, '一个条目释放不能覆盖另一个条目的接管');
        host.seek(3, false, 1);
        assert.equal(suppressed(host), false);
        host.seek(0, true, 1);host.runFrames(1);
        assert.equal(suppressed(host), true, '拖回窗口可重新接管');
        host.setStopped(0, 1);
        assert.equal(suppressed(host), false);
        console.log('ok 多脚本接管 / seek 越窗与重入 / 播放结束恢复');
    }
    {
        for (const code of [
            take + 'throw new TypeError("body failure");',
            '$.root.addEventListener("enterFrame",function(){' + take + 'throw new TypeError("frame failure");});',
            'Utils.interval(function(){' + take + 'throw new TypeError("timer failure");},1,0);',
            'Utils.delay(function(){' + take + 'throw new TypeError("delay failure");},1);',
            'Player.keyTrigger(function(){' + take + 'throw new TypeError("trigger failure");},10000);'
        ]) {
            const host = await create();
            host.append([item('broken', code)]);host.runFrames(6);host.advanceRealTime(10);host.api.pushKey(65,false);
            assert.equal(suppressed(host), false, code);
            assert.equal(host.errors('runtime').length, 1);
            assert.equal(host.frameErrors.length, 0);
        }
        console.log('ok 正文 / enterFrame / Timer / delay / 按键回调报错恢复');
    }
    {
        const host = await create();
        host.append([item('restore', `var layer=${layer};$.root.removeChild(layer);
            Player.keyTrigger(function(){$.root.addChild(layer);},10000);`)]);
        host.runFrames(1);
        assert.equal(suppressed(host), true);
        host.api.pushKey(65,false);
        assert.equal(suppressed(host), false, '显式加回普通弹幕层应释放当前条目的接管');
        host.reset(0, true, 1, true);host.api.setOrdinaryDanmakuBridge(8);
        host.append([item('next', take)]);host.runFrames(1);
        assert.equal(suppressed(host), true);
        assert.equal(events(host).at(-1).version, 8);
        host.reset(0, false, 1, false);host.api.pushKey(65,false);host.advanceRealTime(100);
        assert.equal(suppressed(host), false, 'reset 后旧条目不能继续屏蔽新视频');
        console.log('ok 主动加回 / 内容版本隔离 / reset 清理');
    }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
