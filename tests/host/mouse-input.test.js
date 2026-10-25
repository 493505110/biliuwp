'use strict';

const assert = require('node:assert/strict');
const { loadHost } = require('./retained-mode.test');
const cases = [];
const test = (name, run) => cases.push({ name, run });
const shape = `var box=$.createShape({x:100,y:100,lifeTime:10});
    box.graphics.beginFill(0xffffff,1);box.graphics.drawRect(0,0,80,40);box.graphics.endFill();window.__box=box;`;
async function create(code, duration = 10) {
    const host = await loadHost();
    host.reset(0, true, 1, true);
    host.api.setOrdinaryDanmakuBridge(7);
    host.append([{ id:'mouse', stime:0, duration, lang:'js', code }]);
    host.runFrames(1);
    return host;
}
const tap = (h, x, y, dispatch = true) => h.api.handleTap(x / 800, y / 600, dispatch);
const pointer = (h, action, x, y, down = false, id = 1, version = 7) => h.postHostMessage({
    type:'pointer', version, action, x:x/800, y:y/600, pointerId:id, down
});

test('按钮 onclick 恰好执行一次，暂停时回调新建元件仍归属原条目', async () => {
    const h = await create(`window.__clicks=0;
        window.__button=$.createButton({x:100,y:100,width:100,height:40,text:'click',lifeTime:10,
            onclick:function(e){window.__clicks++;window.__event=e;window.__created=$.createComment('clicked',{lifeTime:10});}});`);
    h.setState(0, false, 1);
    pointer(h,'down',120,120,true);pointer(h,'up',120,120);
    assert.equal(h.sandbox.__clicks,0,'mouseUp 不合成 click');
    assert.equal(tap(h,120,120),true);
    assert.equal(h.sandbox.__clicks,1);
    assert.equal(h.sandbox.__event.target,h.sandbox.__button);
    assert.equal(h.sandbox.__created.ownerItem,h.sandbox.__button.ownerItem);
    assert.equal(tap(h,120,120,false),true,'双击检测不能再派发 click');
    assert.equal(h.sandbox.__clicks,1);
    h.runFrames(1);
    assert.equal(h.errors().length,0);
});

test('无效变换和alpha的元素及其子树不抢点击，恢复后立即可交互', async () => {
    const h=await create('var group=$.createCanvas({x:100,y:100,lifeTime:10});'
        +'var box=$.createShape({parent:group,lifeTime:10});box.graphics.beginFill(0xffffff);'
        +'box.graphics.drawRect(0,0,80,40);box.graphics.endFill();box.addEventListener("click",function(){});window.__group=group;window.__box=box;');
    assert.equal(tap(h,120,120,false),true);
    for(const key of ['scaleX','alpha']) {
        h.sandbox.__group[key]=NaN;
        assert.equal(tap(h,120,120,false),false,'无效的父节点不能让子节点成为目标');
        h.sandbox.__group[key]=1;
        assert.equal(tap(h,120,120,false),true,'有效值恢复后无需重建脚本');
    }
});

test('鼠标事件、冒泡、坐标与取消冒泡', async () => {
    const h=await create(shape+`window.__events=[];
        ['mouseOver','mouseMove','mouseDown','mouseUp','mouseOut'].forEach(function(t){box.addEventListener(t,function(e){window.__events.push([t,e.localX,e.localY,e.buttonDown]);});});
        box.addEventListener('click',function(e){window.__target=e.target;window.__events.push(['child',this.mouseX,this.mouseY]);});
        $.root.addEventListener('click',function(e){window.__events.push(['root',e.stageX,e.stageY]);});`);
    pointer(h,'move',120,115);pointer(h,'down',120,115,true);pointer(h,'up',120,115);tap(h,120,115);
    pointer(h,'leave',900,700);
    assert.deepEqual(JSON.parse(JSON.stringify(h.sandbox.__events)).map(event =>
        event.map(value => typeof value === 'number' ? Math.round(value * 1e6) / 1e6 : value)),[
        ['mouseOver',20,15,false],['mouseMove',20,15,false],['mouseDown',20,15,true],
        ['mouseUp',20,15,false],['child',20,15],['root',120,115],['mouseOut',20,15,false]
    ]);
    assert.equal(h.sandbox.__target,h.sandbox.__box);
    h.sandbox.__box.addEventListener('click',e=>e.stopPropagation());
    tap(h,120,115);
    assert.equal(h.sandbox.__events.at(-1)[0],'child');
    h.sandbox.__box.addEventListener('click',e=>e.stopImmediatePropagation());
    h.sandbox.__box.addEventListener('click',()=>assert.fail('后面的监听必须停止'));
    tap(h,120,115);
});

test('按显示层级选择目标，装饰/隐藏/禁用/透明/摘离元件不抢点击', async () => {
    const h=await create(shape+`window.__hits=[];box.addEventListener('click',function(){window.__hits.push('bottom');});
        var top=$.createButton({x:100,y:100,width:80,height:40,lifeTime:10,onclick:function(){window.__hits.push('top');}});window.__top=top;
        var decoration=$.createComment('decoration',{x:100,y:100,lifeTime:10});`);
    tap(h,120,120);assert.equal(h.sandbox.__hits.at(-1),'top');
    for (const [key,value] of [['visible',false],['enabled',false],['alpha',0],['mouseEnabled',false]]) {
        h.sandbox.__top[key]=value;tap(h,120,120);assert.equal(h.sandbox.__hits.at(-1),'bottom',key);
        h.sandbox.__top[key]=key==='alpha'?1:true;
    }
    h.sandbox.__top.remove();tap(h,120,120);assert.equal(h.sandbox.__hits.at(-1),'bottom');
    assert.equal(tap(h,400,400),false,'空白区域留给播放器');
});

test('嵌套缩放和旋转使用本地形状，拒绝旋转包围盒的空白角', async () => {
    const h=await create(`window.__hits=0;
        var parent=$.createCanvas({x:300,y:100,rotation:90,scaleX:2,scaleY:2,lifeTime:10});
        var child=$.createButton({parent:parent,x:10,y:0,width:80,height:40,lifeTime:10,onclick:function(){window.__hits++;}});window.__parent=parent;`);
    assert.equal(tap(h,260,160),true);
    assert.equal(h.sandbox.__hits,1);
    assert.equal(tap(h,330,160),false);
    h.sandbox.__parent.rotation=45;
    assert.equal(tap(h,260,160),false,'旋转后的外接矩形并非实际点击区');
    assert.equal(tap(h,300,160),true);
    h.sandbox.__parent.scaleX=0;assert.equal(tap(h,300,160),false,'退化矩阵不能命中');
});

test('mouseChildren 控制容器目标，空容器/隐藏孩子/孩子间空隙不形成点击区', async () => {
    const h=await create(`var group=$.createCanvas({x:100,y:100,lifeTime:10});window.__group=group;
        var left=$.createButton({parent:group,width:40,height:40,lifeTime:10});window.__left=left;
        var right=$.createButton({parent:group,x:100,width:40,height:40,lifeTime:10});
        group.addEventListener('click',function(e){window.__target=e.target;});`);
    tap(h,120,120);assert.equal(h.sandbox.__target,h.sandbox.__left);
    h.sandbox.__group.mouseChildren=false;
    tap(h,120,120);assert.equal(h.sandbox.__target,h.sandbox.__group);
    assert.equal(tap(h,170,120),false,'不能用整棵子树的包围盒覆盖空隙');
    h.sandbox.__left.visible=false;assert.equal(tap(h,120,120),false);
});

test('滚动裁剪、舞台遮罩与元素遮罩限制点击区域', async () => {
    const h=await create(shape+`box.addEventListener('click',function(){});box.scrollRect={x:20,y:0,width:20,height:40};`);
    assert.equal(tap(h,110,120),true);assert.equal(tap(h,130,120),false);
    h.sandbox.__box.scrollRect=null;
    h.reset(0,true,1,true);h.api.setOrdinaryDanmakuBridge(7);
    h.append([{id:'masked',stime:0,duration:10,lang:'js',code:shape+`
        box.addEventListener('click',function(){});
        var mask=$.createShape({x:100,y:100,lifeTime:10});mask.graphics.beginFill(0xffffff,1);mask.graphics.drawRect(0,0,20,40);mask.graphics.endFill();
        box.mask=mask;window.__mask=mask;`}]);h.runFrames(1);
    assert.equal(tap(h,110,120),true);assert.equal(tap(h,130,120),false);
    assert.equal(tap(h,110,120,false),true);
});

test('版本隔离、隐藏/停止/reset/seek 越窗清理，取消后其他指针可输入', async () => {
    const h=await create(shape+`window.__downs=0;box.addEventListener('mouseDown',function(){window.__downs++;});`);
    pointer(h,'down',120,120,true,1,6);assert.equal(h.sandbox.__downs,0);
    pointer(h,'down',120,120,true);pointer(h,'down',120,120,true,2);assert.equal(h.sandbox.__downs,1);
    pointer(h,'cancel',0,0);pointer(h,'down',120,120,true,2);assert.equal(h.sandbox.__downs,2);
    h.api.visible(false);assert.equal(tap(h,120,120),false);
    pointer(h,'down',120,120,true,3);assert.equal(h.sandbox.__downs,2);
    h.api.visible(true);pointer(h,'down',120,120,true,3);assert.equal(h.sandbox.__downs,3);
    h.setStopped(0,1);assert.equal(tap(h,120,120),false);
    h.seek(11,false,1);assert.equal(tap(h,120,120),false);
    h.seek(0,true,1);h.runFrames(1);pointer(h,'down',120,120,true,4);assert.equal(h.sandbox.__downs,1,'新激活条目重新计数');
    h.reset(0,true,1,true);assert.equal(tap(h,120,120),false);
    assert.equal(tap(h,NaN,120),false);
});

test('隐藏、取消与离开区域时释放脚本的按住状态，不触发 click', async () => {
    const h=await create(`window.__held=false;window.__clicks=0;
        var b=$.createButton({x:100,y:100,width:80,height:40,lifeTime:10,onclick:function(){window.__clicks++;}});
        b.addEventListener('mouseDown',function(){window.__held=true;});
        b.addEventListener('mouseUp',function(e){window.__held=e.buttonDown;});`);
    for (const action of ['cancel','leave','hide','stop']) {
        pointer(h,'down',120,120,true);
        assert.equal(h.sandbox.__held,true);
        if(action==='hide') { h.api.visible(false);h.api.visible(true); }
        else if(action==='stop') { h.setStopped(0,1);h.setState(0,true,1); }
        else pointer(h,action,120,120);
        assert.equal(h.sandbox.__held,false,action);
        assert.equal(h.sandbox.__clicks,0);
    }
});

test('抛错的 onclick 被隔离，其他条目仍可交互，旧条目监听不残留', async () => {
    const h=await create(`$.createButton({x:100,y:100,width:80,height:40,lifeTime:10,onclick:function(){throw new TypeError('click failure');}});`);
    h.append([{id:'other',stime:0,duration:10,lang:'js',code:`window.__hits=0;$.createButton({x:300,y:100,width:80,height:40,lifeTime:10,onclick:function(){window.__hits++;}});`}]);h.runFrames(1);
    assert.equal(tap(h,120,120),true);assert.equal(h.errors('runtime').length,1);
    tap(h,320,120);assert.equal(h.sandbox.__hits,1);
    assert.equal(h.frameErrors.length,0);
    h.seek(11,false,1);assert.equal(tap(h,320,120),false);
});

async function run() {
    let failed=0;
    for (const {name,run} of cases) {
        try { await run();console.log('ok '+name); }
        catch (error) { failed++;console.error('FAIL '+name+'\n'+error.stack); }
    }
    console.log(`${cases.length-failed}/${cases.length} mouse input tests passed`);
    if(failed) process.exitCode=1;
}
run().catch(error=>{console.error(error);process.exitCode=1;});
