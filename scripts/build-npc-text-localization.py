"""Generate conservative display-only NPC text mappings from audited corpus.

NpcCaptions maps exact displayed menu captions, never jump targets. NpcLines
maps exact full SAY/ELSESAY lines with no dynamic server-expanded variables.
Unmatched text is deliberately left in English. All sources remain unchanged.
"""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def table(text):
    result = {}
    for line in text.strip().splitlines():
        key, value = line.strip().split("=", 1)
        assert key not in result, key
        result[key] = value
    return result


CAPTIONS = table("""
Back=返回
Close=关闭
close=关闭
Go to Main=返回主菜单
Main=主菜单
View=查看
Repair=修理
Buy Back=购回
BuyBack=购回
Exit=离开
Okay.=好的。
I've had enough. Let me go.=我不想继续了，让我离开。
Proceed.=继续。
More=更多
Ask=询问
Sell=出售
Next=下一页
Main Menu=主菜单
Move=前往
Access=打开
Skill List=技能列表
Go to Next=下一页
Price List=价目表
Collect=领取
Send=寄送
Purchase=购买
Cancel=取消
Ok=好的
Buy=购买
Service=服务
Store Pets=寄存宠物
Time=时间
BorderVillage=边境村
MudWall=土城
TaoistSchool=道士学院
Weapon shop=武器店
Accessory shop=首饰店
Armour shop=服装店
Crafting=制作
Prajna Island=般若岛
BichonWall=比奇城
Yes=是
No=否
Rehire=重新雇用
Skip=跳过
Past Bichon=昔日比奇
Seokcho Valley=石草山谷
Serpent Valley=毒蛇山谷
Talk=交谈
Use.=使用。
View Market=查看市场
Woomyon Woods=沃棉森林
Apply for sale of Guild territory=申请出售行会领地
Apply or Cancel=申请或取消
Bichon Wall=比奇城
Book store=书店
Cancel sale of Guild territory=取消出售行会领地
Candles=蜡烛
DungeonEscapes=地牢逃脱卷
Extend=延长
Extend rental period=延长租期
Forge=锻造
Forge Now=立即锻造
Help=帮助
Individual summon=单人召唤
Look at=查看
Mongchon Province=盟重省
RandomTeleports=随机传送卷
RepairOil=修复油
Summon=召唤
Tao School=道士学院
Total summon=全员召唤
TownTeleport=回城卷
Trade=交易
Waste Lands=荒原
Who do you wish to summon?=你想召唤谁？
Reagent store=材料店
Basic control(rooting etc)=基本操作（拾取等）
Storage=仓库
Store=商店
Continue=继续
Previous=上一页
Return=返回
Refine=精炼
Check=查看
Confirm=确认
Accept=接受
Decline=拒绝
""")

LINES = table("""
I will not help an evil person like you...=我不会帮助像你这样的恶人……
MP will be consumed.=施放时会消耗魔法值。
These are the items still available to purchase back.=以下物品仍可购回。
MP and amulet will be consumed.=施放时会消耗魔法值和护身符。
Which item would you like to Buy or Sell?=你想买卖哪件物品？
What Item would you like to buy or sell?=你想买卖什么物品？
What item would you like to buy or sell?=你想买卖什么物品？
Hello traveller, How may I help you?=你好，旅行者，需要我帮忙吗？
Hello traveller, how may I help you?=你好，旅行者，需要我帮忙吗？
What item do you want to store or withdraw?=你想存取什么物品？
Hello traveller. How can i help you?=你好，旅行者，需要我帮忙吗？
A Randomteleport scroll is a magic paper that can=随机传送卷是一种魔法卷轴，能够
For example if you were in a dungeon 1st floor, you will=例如，你在地牢一层使用它时，
The power of Dungeonescape scroll is weak compared with=地牢逃脱卷的效果虽然不如
This will make hunts last longer.=这样能让你在外狩猎更久。
and you can return to a village promptly by using it...=使用它便可迅速返回村庄……
goes down as time goes by.=会随着使用次数逐渐降低。
move to another place on that floor by using the RT.=会被传送到该层的另一个位置。
skill is lost.... however....=制作技艺已经失传……不过……
to certain fields near the last village you visited.=将你送到最近到访村庄附近的野外。
to help you out of a tight situation.=帮助你摆脱险境。
Come back only if u do have one.=等你带来以后再来吧。
Once the skill has been used, you will have to wait to use it again.=技能施放后，需要等待一段时间才能再次使用。
Skill will be ineffective at higher level enemy.=对等级较高的敌人，此技能可能无效。
Would you like to repair a drapery piece?=你想修理衣服吗？
A Dungeonescape scroll is a magic paper that moves you promptly=地牢逃脱卷是一种魔法卷轴，能迅速
A DungeonEscape scroll is a magic paper that moves you promptly=地牢逃脱卷是一种魔法卷轴，能迅速
A RepairOil makes the durabillity of you hand weapon rise,=修复油可以恢复手中武器的持久，
A RepairOil makes the durability of your hand weapon rise.=修复油可以恢复手中武器的持久。
Hello Traveller. What can I do for you?=你好，旅行者，有什么需要吗？
I have heard that it may come randomly from Monsters=听说有些怪物
I have heard that they might come randomly from monsters=听说有些怪物
If situation is dangerous but you still have potions, use this=遇到危险而身上还有药水时，可以使用它
If the situation is dangerous but you still have potions, use this=遇到危险而身上还有药水时，可以使用它
If the weapon at your hand was not expensive,=如果你手中的武器并不贵重，
If the weapon was not expensive,=如果你的武器并不贵重，
Show me the weapon that needs it.=把需要修理的武器拿给我吧。
TownTeleport scrolls carn't be made now because the ancient=如今已经无法制作回城卷，因为古老的
TownTeleport scrolls can't be made now because the ancient=如今已经无法制作回城卷，因为古老的
Would you like to repair a weapon?=你想修理武器吗？
but this oil doesn't special repair your weapon, so=不过，修复油并非特殊修理，因此
This oil doesn't special repair your weapon, so=不过，修复油并非特殊修理，因此
from monsters only=只有怪物才可能掉落。
make you randonly move to another place in the same map.=把你随机传送到当前地图的另一处。
make you randomly move to another place in the same map.=把你随机传送到当前地图的另一处。
remember that then maximum durabillity of the weapon=请注意，武器的持久上限
remember that the maximum durability of the weapon=请注意，武器的持久上限
repair with this oil sometimes during hunting.=可以在狩猎途中偶尔用修复油修理。
repair with this oil occasionally during hunting.=可以在狩猎途中偶尔用修复油修理。
you can not see anything-even your foot for darkness.=你会因黑暗而看不清周围，甚至自己的脚下。
you can not see anything.=你会因黑暗而看不清周围。
Welcome, What can I do for you?=欢迎光临，有什么需要吗？
Candles are needed when it's dark. without a candle=天黑时需要蜡烛照明，没有蜡烛的话，
Candles are needed when it's dark. Without a candle=天黑时需要蜡烛照明，没有蜡烛的话，
Hello, Are you looking for something particular?=你好，你在找什么特别的东西吗？
How can I be of assistance.=有什么可以帮你的吗？
TownTeleport scrolls but it could still save your liefe.=回城卷，但仍能在危急时救你一命。
TownTeleport scrolls but it could still save your life.=回城卷，但仍能在危急时救你一命。
Attacks all monsters in a certain area with meteors of fire falling from the sky.=召唤从天而降的火焰流星，攻击范围内的所有怪物。
Defense power and duration time will depend on the skill level.=防御效果和持续时间取决于技能等级。
I transport men and goods to other places fast and safe.=我可以快速、安全地将人员和货物送往其他地方。
If the caster is attacked while using the skill, it will be cancelled.=施法时受到攻击，技能将被打断。
It has a very long casting time but gives a higher damage than the Blizzard skill.=施法时间较长，但伤害高于暴风雪。
Its duration will be extended according to practice levels and SC power.=持续时间随技能熟练等级和道术提升。
So what do you say?=你意下如何？
The damage inflicted raises with the skill level.=造成的伤害随技能等级提升。
This will burn all the objects within the area.=将灼烧范围内的所有目标。
Welcome. What can I do for you?=欢迎光临，有什么需要吗？
Which place would you like to go?=你想去哪里？
I have been sent here to check the living conditions of people.=我受派来到这里，了解百姓的生活情况。
Unfortunately, it seems that many people are still suffering from=遗憾的是，许多人仍在遭受
Be gone, don't waste my time again!=走吧，别再浪费我的时间！
I am craft lady, specialized in the creation of new items.=我是一名工艺师，专门制作各种物品。
If you also want anything to be made, you may as well ask me.=如果你也有想制作的东西，不妨来问我。
Please select the Recipe you either want to Buy or Sell.=请选择你想买卖的制作配方。
You Dont Have enough {Gold/Gold} to use my Service!=你的{金币/Gold}不足，无法使用这项服务！
poverty. So, I would like to help them with my making skills.=贫困。因此，我希望用自己的手艺帮助他们。
I will buy high quality for high price.=品质好的肉，我愿意出高价收购。
I'll buy it at low price.=我只能出低价收购。
Once you have killed it, press the ALT button, while left clicking=击杀后，按住 ALT 键，并用鼠标左键
To get meat from any of the above, first of all kill the animal.=想从这些动物身上取得肉，首先要将它们击杀。
Using magic to kill an animal burns the meat, reducing its quality to 0.=使用魔法击杀动物会烧坏肉，使品质降为零。
Welcome, what can I do for you?=欢迎光临，有什么需要吗？
You should then see a piece of meat appear in your inventory.=随后，你会在背包里看到一块肉。
but if the meat is stained with soil or burned with fire=但是，如果肉沾上泥土或被火烧坏，
on the dead body for a few seconds.=持续点击尸体数秒。
Just pay the fee then ill escort you to anywhere.=支付费用后，我就可以送你前往目的地。
What Posion's do you wish to buy?=你想购买哪种毒药？
What kind of Poison would you like to purchase?=你想购买哪种毒药？
What would like to purchase?=你想购买什么？
What would you like to sell?=你想出售什么？
You can repair various kinds of Bracelets and Gloves.=这里可以修理各种手镯和手套。
You can repair various kinds of Jewellery.=这里可以修理各种首饰。
You can repair various kinds of Torch's.=这里可以修理各种火把。
Ah your the traveler.. Ive been hearing about.=啊，你就是我听说的那位旅行者。
Item's you are still able to purchase back.=以下物品仍可购回。
Welcome, how may I help you?=欢迎光临，需要我帮忙吗？
You are not the right level or to poor.=你的等级不符，或金币不足。
Sorry you don't meet the requirements to store your pets!=抱歉，你尚未满足寄存宠物的条件！
You need to bring me your token!=你需要把凭证带来！
Before deafeating the monsters,=在击败这些怪物之前，
You cannot proceed to the next stage.=你不能进入下一关。
You haven't beat them yet.=你还没有击败它们。
But there are still more to go.=不过，后面还有更多挑战。
Will you continue the challenge?=你要继续挑战吗？
You defeated them all already.=你已经击败了所有怪物。
Hello!=你好！
You can't have any activate pets to use this feature=使用此功能前，请先收回所有出战宠物。
Nothing happens.=什么也没有发生。
Buy.=购买。
Sell.=出售。
How can I help you?=有什么可以帮你的吗？
You don't have enough ingredients.=你的材料不足。
I can store certain creatures for you..=我可以帮你寄存某些召唤生物。
I will give you a token to retrieve them whenever you fancy=我会给你一张凭证，凭它可以随时取回。
Make sure you have your token!=请务必带好凭证！
Repair.=修理。
So you want to retreive your pets?=你想取回寄存的宠物吗？
Special repair.=特殊修理。
THEY MUST ALL BE LEVEL 7 AND STILL ALIVE to store them and=寄存时，它们必须全部达到七级且仍然存活，
You have no business here...=这里没有你要办的事……
all be the same type of creature.=并且必须是同一种生物。
Basic control:=基本操作：
Ask me anytime, I will be glad to guide you.=有问题随时来问我，我很乐意为你指路。
Anything I can help with?=有什么可以帮你的吗？
Template.=模板。
""")

MENU = re.compile(r"<([^<>]*?)/(@[^<>]+)>")
COLOR = re.compile(r"/([A-Za-z][A-Za-z0-9]*)}")
TOKEN = re.compile(r"<\$[^<>]+>|\[(?:MONSTER|NPC|ITEM):[^\]]+\]|\{\d+(?:[^{}]*)\}",re.I)


def signature(text):
    return (MENU.findall(text) and [m[1] for m in MENU.findall(text)] or [],
            COLOR.findall(text), TOKEN.findall(text))


def main():
    corpus_path = ROOT / ".ai/npc-visible-text.json"
    corpus = json.loads(corpus_path.read_text(encoding="utf-8"))
    world = json.loads((ROOT / "Localization/DbLanguage.zh-CN.json").read_text(encoding="utf-8-sig"))
    known_captions = {c["label"] for c in corpus["menuLabels"]}
    captions = {k:v for k,v in CAPTIONS.items() if k in known_captions}
    # Exact known world names, and skill names with a numeric level suffix.
    names = world["Maps"] | world["Magics"]
    for caption in known_captions:
        if caption in captions:
            continue
        if caption in names:
            captions[caption] = names[caption]
            continue
        match = re.fullmatch(r"(.+?)(\s+)(\d+)",caption)
        if match and match[1] in world["Magics"]:
            captions[caption] = world["Magics"][match[1]]+match[2]+match[3]
    lines = {}
    for entry in corpus["englishLines"]:
        original = entry["sourceText"]
        # Client receives dynamic tokens already expanded, so raw templates
        # cannot be matched exactly there. Do not include them in this pack.
        if entry["features"]["hasVariable"] or entry["features"]["unbalancedMarkup"]:
            continue
        if original in LINES:
            lines[original] = LINES[original]
        elif original in captions:
            lines[original] = captions[original]
        else:
            matches = list(MENU.finditer(original))
            outside = MENU.sub("",original)
            # Generate only whole menu-only lines for which every caption is
            # known. Never rewrite mixed narrative or apply substring matching.
            if matches and not re.search(r"[A-Za-z]",outside) and all(m[1] in captions for m in matches):
                lines[original] = MENU.sub(lambda m:"<"+captions[m[1]]+"/"+m[2]+">",original)
    for original,translation in lines.items():
        assert signature(original)==signature(translation),(original,translation)
        assert original.count("\n")==translation.count("\n"),(original,translation)
    for caption,translation in captions.items():
        assert not any(c in translation for c in "<>{}"),(caption,translation)
    output={"NpcCaptions":dict(sorted(captions.items())),"NpcLines":dict(sorted(lines.items()))}
    covered_entries=[e for e in corpus["englishLines"] if e["sourceText"] in lines]
    menu_covered=[m for m in corpus["menuLabels"] if m["label"] in captions]
    coverage={"sourceCorpus":".ai/npc-visible-text.json","dictionary":"Localization/NpcText.zh-CN.json",
       "scope":"Conservative exact client display mappings. Unmatched narrative is left unchanged; no regex translation of player text or internal IDs.",
       "NpcCaptions":{"translated":len(captions),"corpusUniqueLabels":len(corpus["menuLabels"]),"occurrencesCovered":sum(len(m["occurrences"]) for m in menu_covered),"corpusOccurrences":sum(len(m["occurrences"]) for m in corpus["menuLabels"])},
       "NpcLines":{"translated":len(lines),"corpusUniqueExactEnglishLines":len(corpus["englishLines"]),"occurrencesCovered":sum(len(e["occurrences"]) for e in covered_entries),"corpusOccurrences":corpus["summary"]["visibleEnglishLineOccurrences"],"priorityUniqueLinesCovered":sum(any(o["priority"] for o in e["occurrences"]) for e in covered_entries),"priorityUniqueLinesTotal":corpus["summary"]["priority"]["uniqueExactScriptLines"]},
       "validation":{"jumpTargetsChanged":0,"colorCodesChanged":0,"tokensChanged":0,"newlinesChanged":0,"dynamicServerTemplatesIncluded":0},
       "integrationNotes":["Apply NpcLines once before NPCDialog.NewText parses buttons/colours; preserve original leading/trailing whitespace when looking up a trimmed line.","Map exact NpcCaptions on captured display text only, in both normal buttons and big buttons; keep action/click targets unchanged.","NPCSegment.ParseSay resolves <$...> variables before sending. This client pack intentionally excludes variable-containing whole-line templates.","NpcCaptions and NpcLines coverage overlap and must not be added together.","Coverage inventories archived/unreferenced script variants as well as direct NPC references; it is not live gameplay acceptance.","Quest descriptions are inventoried in the corpus but are not translated comprehensively by this NPC starter pack."]}
    directory=ROOT/"Localization"
    (directory/"NpcText.zh-CN.json").write_text(json.dumps(output,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    (directory/"NpcText.zh-CN.coverage.json").write_text(json.dumps(coverage,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(coverage,ensure_ascii=False,indent=2))


if __name__=="__main__":
    main()
