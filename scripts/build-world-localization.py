"""Build exact display-name translations from the exported Crystal database.

Internal names, IDs, file names and scripts are never modified.  Numbered maps
use explicit area names plus a floor/section suffix; NPCs use explicit role and
personal-name tables while retaining underscores used by the client for lines.
Names without a known classic Chinese equivalent use semantic translations or
phonetic names, rather than claiming that later content is a classic 1.76 area.
"""
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


MAPS = table("""
1FofDeadMine=废矿一层
2FofInn=客栈二楼
Academy=学院
AccessoryStore=首饰店
AmethystRoom=紫晶密室
AncientNaturalCave=远古天然洞穴
AncientSTombEntrance=远古石墓入口
AncientTemple=远古神殿
AncientTempleEntrance=远古神殿入口
AncientZumaLobby=远古祖玛大厅
AngledStoneTombEntrance=石墓入口
ArchersHideout=弓箭手藏身处
Armory=军械库
B1ContestGround=比武场地下一层
B1Residence=民宅地下一层
B1ofMine=矿洞地下一层
B2ofDeadMine=废矿地下二层
Barracks=兵营
BeautySaloon=美容院
BichonProvince=比奇省
BlackDragonDungeon=黑龙地牢
BlackSnakePalace=黑蛇宫殿
Blacksmith=铁匠铺
BloodGorge=血色峡谷
BloodLand=血地
BloodPass=血色通道
BookStore=书店
BorderRestaurant=边境酒家
CastleGi-Ryoong=吉龙城
CastleRuins=城堡遗迹
ConnectionCave=连接洞穴
ConnectionPath=连接通道
ContestGround=比武场
CraftingVillage=工匠村
DYMine4Boss=道馆矿洞四区首领室
DY_Craft=道馆工坊
DY_Head=道馆首领府
DY_MedStorage=道馆药材库
DY_Pharmacy=道馆药房
DY_Smithy=道馆铁匠铺
DY_Weapon=道馆武器店
DY_Wearhouse=道馆仓库
DangerousCavern=险恶洞穴
DarkDevilPalace=黑暗魔殿
DarkForest=黑暗森林
DarkSwamp=黑暗沼泽
DeadForest=死亡森林
DeadMineEntrance=废矿入口
DogYoArena=道馆竞技场
DogYoHyun=道馆县
DogYoMineLift=道馆矿井升降台
DogYoMineLobby=道馆矿洞大厅
DrapersStore=布衣店
DraperyStore=绸缎庄
Drug_Store=药材店
EastOmaGorge=半兽人峡谷东部
EastOmaPass=半兽人通道东部
EastOmaValley=半兽人山谷东部
EastPass=东部通道
EasternLostCave=失落洞穴东部
EastofDeadMine=废矿东部
EvilMirPalace=魔龙宫殿
FatallyPoisonousCave=剧毒洞穴
FisherManHouse=渔夫之家
ForgottenCity=遗忘之城
GTCourt=行会领地庭院
GameMaster=管理员区域
GeneralStore=杂货店
GeneralsCamp=将军营地
GhoulCave=尸王殿
Great_TaoTomb=恶魔祭坛
GroceryStore=杂货铺
HauntedForest=幽灵森林
HellFire_KingsRoom=地狱火王殿
HellOverpass=地狱天桥
HiddenRoom=隐藏密室
HwanMaJin=幻魔阵
IceHellCavern=冰狱洞穴
IceHellPass=冰狱通道
IceHellTemple(N)=冰狱神殿（北）
IceHellTemple(S)=冰狱神殿（南）
IceHellTemple_KR=冰狱王殿
InfiniteBout=无尽试炼
Inn=客栈
InnerCastle=内城
InnerWall=内城墙
ItemDepository=物品保管所
Item_Lab=物品研究室
KingsTomb=国王陵墓
Kitchen=厨房
Laundry=洗衣房
Library=藏书阁
LifeDeathCoffin=生死棺
LightningCave=雷电洞穴
LobbyofZumaTemple=祖玛寺庙大厅
LostCave(MainCave)=失落洞穴（主洞）
LostMiddleCave=失落洞穴中部
LunarRoom=月之密室
MageHouse=法师之家
MeatStore=肉铺
MedicineRoom=药房
Mill=磨坊
MoltenRockCave=熔岩洞穴
MongchonProvince=盟重省
MysteryCave=神秘洞穴
N_1FofDungeon=地牢一层北
N_2FofDungeon=地牢二层北
NaturalCave=天然洞穴
NobleHogPalace=贵族猪殿
NorthernLostCave=失落洞穴北部
OmaGorge=半兽人峡谷
OmaValley=半兽人山谷
OreStoragePlace=矿石仓库
OverPass=天桥
Palace=皇宫
PastBichon=昔日比奇
PenalCavern=刑罚洞窟
PotionStore=药店
PrajnaIsland=苍月岛
PrajnaStoneCave(E)_3F=骨魔洞东三层
PrajnaStoneCave(W)_3F=骨魔洞西三层
PrajnaTemple=牛魔寺庙
PrajnaTemple(E)_5F=牛魔寺庙东五层
PrajnaTemple(W)_5F=牛魔寺庙西五层
PrajnaTempleLobby=牛魔寺庙大厅
Prison=监狱
PrisonHall=监狱大厅
PrivateHouse=民宅
PurgatoryHall=炼狱大厅
ReagentStore=炼金材料店
RedCavern_KR=赤色洞窟王殿
RedMoonRoom=赤月魔穴
Rev.Tao_Office=道长居所
Ruins=遗迹
S_1FofDungeon=地牢一层南
S_ValleyDeadMine=山谷南部废矿
S_ValleyTavern=山谷南部酒馆
SabukSecretGate=沙巴克密道入口
SacredFoxHill=圣狐山
SacredFoxTemple=圣狐神殿
SealedMaze=封印迷宫
SeokchoValley=石草山谷
SerpentValley=毒蛇山谷
Shrine=神庙
SoleSpiritHall=孤魂殿
SouthernLostCave=失落洞穴南部
SouthofDeadMine=废矿南部
StoneCarvedStream=石刻溪流
StoneLanternRoom=石灯密室
StrangeRocksPath=怪石通道
StrangeRocksRoom=怪石密室
Swamp=沼泽
TacticalMaze=战术迷宫
Tavern=酒馆
TheSnowLair=冰雪巢穴
TownHall=城镇大厅
TrainingHall=训练馆
Treepath=林间小径
VillageChiefHouse=村长之家
ViperCave=毒蛇洞穴
ViperMaze=毒蛇迷宫
ViperPath=毒蛇通道
W_1FofDungeon=地牢一层西
W_2FofDungeon=地牢二层西
WaitingRoom=等候室
Warehouse=仓库
WasteLands=荒原
WeaponStore=武器店
WestOmaGorge=半兽人峡谷西部
WestOmaPass=半兽人通道西部
WestOmaValley=半兽人山谷西部
WestPass=西部通道
WesternLostCave=失落洞穴西部
WhiteDragonHillSide=白龙山坡
WhiteDragonPassage=白龙通道
WhiteValley=白色山谷
WoomaPalace=沃玛大殿
WoomaTemple=沃玛寺庙
WoomaTempleEntrance=沃玛寺庙入口
WoomyonWoods(N)=沃玛森林（北）
WoomyonWoods(S)=沃玛森林（南）
YimoogiNest=伊莫吉巢穴
ZumaMaze=祖玛迷宫
ZumaPalace=祖玛大殿
ZumaTemple=祖玛寺庙
ZumaTempleLobby_7F=祖玛寺庙七层大厅
""")

MAP_FLOORS = table("""
AncientPrajna=远古苍月
AncientSTomb=远古石墓
AncientTemple=远古神殿
AncientZuma=远古祖玛
BlackDragonDungeon=黑龙地牢
HellCavern=地狱洞穴
HellFire=地狱火窟
InsectCave=虫洞
Lunar=月之洞窟
MineralMine=矿区
OmaCave=兽人古墓
PrajnaStoneCave=骨魔洞
PrajnaTemple=牛魔寺庙
RedCavern=赤色洞窟
RedValley=赤色山谷
SnowCavern=冰雪洞窟
SwampCavern=沼泽洞穴
TrollMine=巨魔矿洞
WoomaTemple=沃玛寺庙
ZumaTemple=祖玛寺庙
""")

NPC_ROLES = table("""
Accessory=首饰商
Administrator=管理官
Alchemist=炼金术士
Assistant=助手
BichonInspector=比奇巡查官
BichonSoldier=比奇士兵
BichonWall=比奇城墙
Blacksmith=铁匠
Book=书商
BorderVillage=边境村
Captain=队长
CaveGuide=洞穴向导
Challange=挑战
Commander=指挥官
CraftingVillage=工匠村
CraftsLady=工艺师
Drapery=布衣商
Emperor=皇帝
Examiner=考官
Excavenger=挖掘者
FishMonger=鱼贩
GM=管理员
GT=行会领地
Gt=行会领地
GTMerchant=行会商人
GTStore=行会商店
General=将军
Grocery=杂货商
Hairdresser=理发师
HighAssassin=高阶刺客
HighPriest=大祭司
InnKeeper=客栈掌柜
Inspector=巡查官
Investigator=调查员
John=约翰
LeadTrainer=总教官
LeftEscort=左护卫
Librarian=藏书管理员
Lottery=彩票商
Luke=卢克
Master=大师
MasterMK=武学大师
MasterMage=大法师
MaterialDealer=材料商
Merchant=商人
MirGuide=传奇向导
MongchonDelegate=盟重使者
MongchonScout=盟重斥候
MudWall=土城
Mysterious=神秘人
OldFisherman=老渔夫
PetMaster=宠物师
PotionShop=药商
Premium=珍品商人
Prison=监狱
Protector=守护者
RightEscort=右护卫
Sailor=船夫
Sir=先生
Solider=士兵
Specialist=专家
SquadLeader=小队长
StableGirl=马倌姑娘
Storage=保管员
SubjagationManager=讨伐管理员
SubjugationLead=讨伐领队
Teleport=传送员
TheWatcher=守望者
Transport=运输员
Traveller=旅行者
TravellingMerchant=游商
TrustMerchant=寄售商人
VillageChief=村长
VulnerableSon=体弱的儿子
Wanderer=流浪者
Warehouse=仓库管理员
WickedTrader=奸商
WiseFisherman=智者渔夫
""")

NPC_NAMES = table("""
Abbigale=阿比盖尔
Albert=阿尔伯特
Alex=亚历克斯
Alfie=阿尔菲
Alfred=阿尔弗雷德
Alice=爱丽丝
Amanda=阿曼达
Andrew=安德鲁
Andy=安迪
Anne=安妮
Anthony=安东尼
Armour=衣甲
Awakening=觉醒
Bart=巴特
Belina=贝琳娜
Belt=腰带
Betty=贝蒂
Bill=比尔
Board=告示牌
Book=技能书
Boot=靴子
Bracelet=手镯
Bradley=布拉德利
Brian=布莱恩
Brittney=布里特妮
Bruce=布鲁斯
Bull=布尔
BulletinBoard=公告栏
Carl=卡尔
Carlos=卡洛斯
Carratt=卡拉特
Chandler=钱德勒
Charley=查理
Charlotte=夏洛特
Chris=克里斯
Christine=克里斯汀
Cindy=辛迪
Clair=克莱尔
Clara=克拉拉
Cloud=克劳德
Cook=库克
CraftingMaterial=制作材料
Damian=达米安
Daniel=丹尼尔
David=大卫
Dean=迪恩
Debora=黛博拉
Denzel=丹泽尔
Derby=德比
Deric=德里克
Diana=戴安娜
Dolby=多尔比
Don=唐
Dr.Hwa=华大夫
Dr.Kim=金大夫
Dustin=达斯汀
Eddie=埃迪
Edward=爱德华
Edwin=埃德温
Elijah=以利亚
Emily=艾米莉
Eric=埃里克
Eugene=尤金
Far=法尔
Fishing=钓鱼
Frank=弗兰克
Gem=宝石
Gerald=杰拉尔德
Gilbert=吉尔伯特
Glenn=格伦
Gordon=戈登
Grace=格蕾丝
Graeme=格雷姆
Grim=格里姆
Grimchamp=格里姆昌普
Grocery=杂货
Guard=守卫
Hans=汉斯
Harrison=哈里森
Harry=哈里
Harvey=哈维
Heather=希瑟
Helen=海伦
Helmet=头盔
Hubert=休伯特
Huxley=赫胥黎
Jack=杰克
Jacob=雅各布
Jake=杰克
James=詹姆斯
Jamie=杰米
Jane=简
Janey=珍妮
Jang=张师傅
Jason=杰森
Jeffery=杰弗里
Jene=吉恩
Jennifer=詹妮弗
Jerald=杰拉尔德
Jerry=杰里
Jessica=杰西卡
Jim=吉姆
Joffrey=乔弗里
John=约翰
Jon=乔恩
Joseph=约瑟夫
Jude=裘德
Julia=朱莉娅
Julie=朱莉
June=琼
Keith=基思
Kelly=凯莉
Ken=肯
Kevin=凯文
Kieron=基伦
Kim=金
Kimberly=金伯莉
Kristin=克里斯汀
Kurtis=柯蒂斯
Kyle=凯尔
Larry=拉里
Laura=劳拉
Louise=露易丝
Luke=卢克
Manager=管理
Martin=马丁
Martyn=马丁
Mary=玛丽
Maximus=马克西穆斯
Meat=肉类
Melissa=梅丽莎
Michael=迈克尔
Michelle=米歇尔
Milton=米尔顿
Mira=米拉
Mogu=莫古
Monica=莫妮卡
Mount=坐骑
Mr.Wang=王先生
Murray=默里
Necklace=项链
Nicole=妮可
Nothing=空白
OldMan=老人
Olivia=奥莉维娅
Ore=矿石
Paddy=帕迪
Patrick=帕特里克
Paula=葆拉
Peddler=小贩
Peggy=佩吉
Perry=佩里
Peter=彼得
Portal=传送门
Potion=药水
Quest=任务
Rachel=蕾切尔
Raymond=雷蒙德
Reece=里斯
Richard=理查德
Ring=戒指
Robert=罗伯特
Robin=罗宾
Roger=罗杰
Ruben=鲁本
Rupert=鲁珀特
SabukOfficer=沙巴克官员
Sam=山姆
Samuel=塞缪尔
Sandford=桑福德
Sandra=桑德拉
Sarah=莎拉
Satin=萨廷
Schwartz=施瓦茨
Scott=斯科特
Script=脚本物品
Shinsu(Jude)=神兽（裘德）
Shok=肖克
Sigmund=西格蒙德
Sir.Kevin=凯文爵士
Smith=史密斯
Soho=索霍
Stanislav=斯坦尼斯拉夫
Stephen=斯蒂芬
Steven=史蒂文
Steward=管家
Stone=石头
Stuart=斯图尔特
Susan=苏珊
Teleporter=传送
Terry=特里
Thompson=汤普森
Tiffany=蒂芙尼
Tony=托尼
Travis=特拉维斯
Vicky=维姬
Victoria=维多利亚
Vincent=文森特
Vivian=薇薇安
Wa=瓦
Walter=沃尔特
Wayne=韦恩
Weapon=武器
Whitney=惠特尼
Yang=杨
Yu=余
""")

NPC_SINGLE = table("""
Apprentice=学徒
Ashes=灰烬
BigTaoist=大道士
Bones=骸骨
BrokenCarriage=损坏的马车
ChaosNode=混沌节点
CrushedBones=碎骨
GTTransporter=行会传送员
GuardianRock=守护岩石
LostSoul=迷失的灵魂
MissDo=多小姐
MissMi=米小姐
MissRe=蕾小姐
Monument=纪念碑
MysteriousPillar=神秘石柱
MysteriousStone=神秘石
OddOldMan=古怪老人
OldSkeleton=陈旧骸骨
OldSkull=陈旧头骨
Pillar=石柱
Proceeder=引导员
Signpost=路标
SkeletonPile=骸骨堆
SkullPile=头骨堆
Stairs=阶梯
StrangeMan=陌生人
StrangePillar=奇异石柱
TimeStone=时光石
TrainerTaoist=道士教官
WierdPillar=怪异石柱
""")

MAGICS = table("""
BackStep=后撤步
BattleCry=战吼
BindingShot=束缚箭
BladeAvalanche=剑刃风暴
BlessedArmour=神圣战甲术
Blink=闪现
Blizzard=暴风雪
CatTongue=猫舌术
Concentration=凝神
CounterAttack=反击
CresentSlash=新月斩
CrippleShot=致残箭
CrossHalfMoon=十字半月
Curse=诅咒术
DarkBody=暗影之躯
DelayedExplosion=延迟爆炸
DoubleShot=二连射
DoubleSlash=双重斩
ElectricShock=诱惑之光
ElementalBarrier=元素屏障
ElementalShot=元素箭
EnergyRepulsor=气功波
EnergyShield=真气护盾
Entrapment=擒龙手
ExplosiveTrap=爆炸陷阱
FatalSword=致命剑术
Fencing=基本剑术
FireBall=火球术
FireBang=爆裂火焰
FireBounce=弹射火焰
FireBurst=爆炎术
FireWall=火墙
FlameDisruptor=灭天火
FlameField=火焰领域
FlamingSword=烈火剑法
FlashDash=闪光突袭
Focus=专注
FrostCrunch=寒冰掌
Fury=狂怒
GreatFireBall=大火球
HalfMoon=半月弯刀
Hallucination=幻觉术
Haste=疾速
Healing=治愈术
HealingCircle=治疗法阵
HeavenlySword=天剑术
HellFire=地狱火
Hemorrhage=放血
Hiding=隐身术
IceStorm=冰咆哮
IceThrust=冰刺术
ImmortalSkin=不朽之肤
LightBody=轻身术
Lightning=疾光电影
LionRoar=狮子吼
MPEater=噬魔术
MagicBooster=魔力强化
MagicShield=魔法盾
MassHealing=群体治愈术
MassHiding=集体隐身术
Meditation=冥想
MentalState=心境
MeteorShower=流星雨
MeteorStrike=流星火雨
Mirroring=分身术
MoonLight=月光术
MoonMist=月雾术
NapalmShot=燃烧箭
OneWithNature=天人合一
PetEnhancer=召唤兽强化
Plague=瘟疫术
PoisonCloud=毒云术
PoisonShot=毒箭
PoisonSword=毒剑术
Poisoning=施毒术
Portal=传送门
ProtectionField=护身领域
Purification=净化术
Rage=暴怒
Reincarnation=复活术
Repulsion=抗拒火环
Revelation=心灵启示
ShoulderDash=野蛮冲撞
SlashingBurst=开天斩
Slaying=攻杀剑术
SoulFireBall=灵魂火符
SoulShield=幽灵盾
SpiritSword=精神力战法
StoneTrap=石化陷阱
StormEscape=风暴逃脱
StraightShot=直射
SummonHolyDeva=召唤月灵
SummonShinsu=召唤神兽
SummonSkeleton=召唤骷髅
SummonSnakes=召唤蛇群
SummonToad=召唤蟾蜍
SummonVampire=召唤吸血鬼
SwiftFeet=疾行
Teleport=瞬息移动
Thrusting=刺杀剑术
ThunderBolt=雷电术
ThunderStorm=地狱雷光
Trap=陷阱术
TrapHexagon=困魔咒
TurnUndead=圣言术
TwinDrakeBlade=双龙斩
UltimateEnchancer=无极真气
VampireShot=吸血箭
Vampirism=噬血术
""")

QUESTS = table("""
!Attack Oma=讨伐半兽人！
Abnormal Creatures=异常生物
Ancient Mobs=远古怪物
AncientOmaCavern=远古兽人古墓
Antique Book=古书
Assistant's Request=助手的请求
Bad News=坏消息
Bat Wings=蝙蝠翅膀
Bloody Sword=染血之剑
Boar Tooth=野猪獠牙
Boss Mob Hunt=猎杀首领
Boss Mob of Woomyon=沃玛的怪物首领
Call from the Dead=亡者的呼唤
Check Supply Path=检查补给路线
Chestnut Request=栗子委托
Clean Up=清剿
Collect Information=收集情报
Cook Book Delivery=送达食谱
Cooking Beef Rib Stew=烹制炖牛排
CorpsFlower=尸花
CraftLady's CanniTea=工艺师的卡尼茶
Cure the Insomnia=治疗失眠
Danger (WoomyonWoods)=沃玛森林的危险
Danger in the Cave=洞穴中的危险
Dangerous Skeleton=危险的骷髅
Dangerous as ever=危险依旧
Dark Shadow=黑暗阴影
Darkness from the Mines=矿洞中的黑暗
Deadly Attack=致命袭击
Deep into the Woods=深入森林
Deliver CanniTea=送达卡尼茶
Deliver Repair Oil=送达修复油
Deliver Supplies to the Border=运送边境补给
Deliver the Ring=送达戒指
Eliminate WhiteBoar=消灭白野猪
Emperors Problem=皇帝的烦恼
Errands=跑腿委托
Exterminate=歼灭
Find Ebony Fruit=寻找乌木果
Find More Antique Books=寻找更多古书
Find Passage to Ruins=寻找通往遗迹的道路
Find my Friend=寻找我的朋友
Find the Web=寻找蛛网
Finding a Friend's Remains=寻找友人的遗骸
Finding a Lost Item=寻找遗失物品
Force Recruit=强制征募
Forest Yeti's Threat=森林雪人的威胁
Free the Souls from Zombies=解救僵尸的灵魂
Gathering Material=收集材料
Gathering of Bones=收集骨头
Grandads Request=爷爷的请求
Greetings to the Old Man=向老人问好
Guards from Prajna=苍月来的守卫
Help Blacksmith Jang=帮助张铁匠
Help Needed=寻求帮助
Help Sandford=帮助桑福德
Help the Farmers=帮助农夫
Help the Lumberer=帮助樵夫
Hidden Box=隐藏的箱子
Holy Sword Quest=圣剑之旅
Hunt CannibalPlants=猎杀食人植物
Hunt for the Butcher=为屠夫狩猎
Ingredient from Prajna=来自苍月的材料
Ingredients for CanniTea=卡尼茶的材料
Ingredients for Snake Wine=蛇酒的材料
Ingredients for the Antidote=解毒剂的材料
Insomnia=失眠
Last Words=遗言
Letters to Parents=给父母的信
Locate the Carriage=寻找马车
Lost Item=遗失物品
Lost Ring=遗失的戒指
Lost Soul=迷失的灵魂
Lost Soul's Remains=迷失灵魂的遗骸
Lost Sword=遗失的剑
Material for Clothing=制衣材料
Medicine Research=药物研究
Meet the Archer Instructor=拜访弓箭手教官
Meet the Assassin Instructor=拜访刺客教官
Meet the Mage Instructor=拜访法师教官
Meet the Taoist Instructor=拜访道士教官
Meet the Warrior Instructor=拜访战士教官
Meet with Mr.Wang=拜访王先生
Message to BichonExaminer=给比奇考官的消息
Messenger=信使
Missing Materials=遗失的材料
Mobs from Prajna=来自苍月的怪物
Monster Hunt in Wooma=沃玛猎魔
New Poison=新型毒药
News from a Friend=友人的消息
Part Time Job=临时工作
Poisoned Friend=中毒的朋友
Rare Medicine=珍稀药材
Rare Ore=稀有矿石
Red Snake's Teeth=红蛇的牙齿
Report to the Manager=向管理员报告
Research Report=研究报告
Restocking Supplies=补充物资
Return the Necklace=归还项链
Returning a Book=归还书籍
Revenge on Zombies=向僵尸复仇
Runaway Student=出走的学生
Saftey of Secret Path=确保密道安全
Save Prajna=拯救苍月岛
Save the Cursed Souls=拯救受诅咒的灵魂
Secret Path=秘密通道
Secret Recipe Delivery=送达秘方
Secret of the Skull=头骨的秘密
SerpentValley=毒蛇山谷
Shipping Relics=运送遗物
Sister's Ring=姐妹的戒指
Skeleton Bones=骷髅的骨头
Skeleton Hunt 1=猎杀骷髅（一）
Skeleton Hunt 2=猎杀骷髅（二）
Skull Collector=头骨收集者
Smith's 2nd Test=铁匠的第二次考验
Stolen Book=被盗的书
Talk with the Butcher=与屠夫交谈
Test for FatalSword Skill=致命剑术试炼
Test for Focus Skill=专注技能试炼
Test for the Fencing Skill=基本剑术试炼
Test for the Fireball Skill=火球术试炼
Test for the Healing Skill=治愈术试炼
The Best Healthy Food=最佳养生食物
The CraftLady's Request=工艺师的请求
The Smith's 1st Test=铁匠的第一次考验
The VillageChief's Request=村长的请求
Threats to Mongchon=盟重面临的威胁
To Bichon=前往比奇
Trip to Mongchon=盟重之旅
Uniform Order Status=制服订单进展
Visit the VillageChief=拜访村长
Wanted!=悬赏！
Way to Mongchon=通往盟重之路
White Serpent=白蛇
Wine Delivery=送酒
Wipe Out=扫荡
Zombies Attack on Carriage=僵尸袭击马车
Zombies Hunt=猎杀僵尸
Zombies in MineralMine=矿区的僵尸
Zombies in the Mines=矿洞中的僵尸
""")


def translate_map(name):
    if name in MAPS:
        return MAPS[name]
    match = re.fullmatch(r"(.+)_(\d+)F", name)
    if match and match[1] in MAP_FLOORS:
        return f"{MAP_FLOORS[match[1]]}{match[2]}层"
    match = re.fullmatch(r"B(\d+)_AngledStoneTomb", name)
    if match:
        floors = ["", "一层", "二层", "三层", "四层", "五层", "六层", "七层"]
        idx = int(match[1])
        floor_str = floors[idx] if idx < len(floors) else f"{idx}层"
        return f"石墓{floor_str}"
    match = re.fullmatch(r"DYMine(\d)(\d)", name)
    if match:
        return f"道馆矿洞{match[1]}区{match[2]}号"
    match = re.fullmatch(r"DogYoMineLobby(\d)", name)
    if match:
        return f"道馆矿洞{match[1]}区大厅"
    match = re.fullmatch(r"GT(\d)F", name)
    if match:
        return f"行会领地{match[1]}层"
    raise KeyError(f"Untranslated map: {name}")


def translate_npc(name):
    if name in NPC_SINGLE:
        return NPC_SINGLE[name]
    role, personal = name.split("_", 1)
    # Leading '_' is an intentional empty first line for _Shinsu(Jude).
    return f"{NPC_ROLES[role] if role else ''}_{NPC_NAMES[personal]}"


def main():
    data = json.loads((ROOT / ".ai/localization-data.json").read_text(encoding="utf-8-sig"))
    names = {s: sorted({x["Title" if s == "Maps" else "Name"] for x in data[s]})
             for s in ("Maps", "Npcs", "Magics", "Quests")}
    out = {"Items": {}, "Monsters": {},
           "Maps": {n: translate_map(n) for n in names["Maps"]},
           "Npcs": {n: translate_npc(n) for n in names["Npcs"]},
           "Magics": {n: MAGICS[n] for n in names["Magics"]},
           "Quests": {n: QUESTS[n] for n in names["Quests"]}}
    # Other tasks own these two sections.  Leave them empty on first generation,
    # but preserve any translations merged later when regenerating world names.
    dictionary_path = ROOT / "Localization/DbLanguage.zh-CN.json"
    if dictionary_path.exists():
        existing = json.loads(dictionary_path.read_text(encoding="utf-8-sig"))
        for section in ("Items", "Monsters"):
            out[section] = existing.get(section, {})
    coverage = {
        "source": ".ai/localization-data.json",
        "dictionary": "Localization/DbLanguage.zh-CN.json",
        "scope": "Exact display-name keys only; Items and Monsters are delegated separately.",
        "translationNotes": [
            "Classic skills and well-known places use familiar Chinese names.",
            "Later/Euro-specific names use semantic translations or phonetic names; no claim of 1.76 content conversion.",
            "Numbered map names are generated from explicit area mappings and floor/section patterns.",
            "NPC names combine explicit role and personal-name tables and preserve every underscore line separator.",
            "NPC dialogue and quest descriptions are outside this name-dictionary scope.",
            "Translations have been statically validated, not visually accepted in game."
        ],
        "sections": {}
    }
    for section in names:
        values = out[section]
        missing = sorted(set(names[section]) - values.keys())
        extra = sorted(values.keys() - set(names[section]))
        untranslated = [k for k,v in values.items() if not re.search(r"[\u4e00-\u9fff]",v)]
        latin = [k for k,v in values.items() if re.search("[A-Za-z]",v)]
        lines = [k for k,v in values.items() if section == "Npcs" and k.count("_") != v.count("_")]
        coverage["sections"][section] = {"records": len(data[section]), "uniqueKeys": len(names[section]),
            "translated": len(values), "missing": missing, "extra": extra,
            "withoutChinese": untranslated, "latinRemainders": latin, "npcLineSeparatorMismatches": lines}
        assert not (missing or extra or untranslated or latin or lines), coverage["sections"][section]
    directory = ROOT / "Localization"
    directory.mkdir(exist_ok=True)
    (directory / "DbLanguage.zh-CN.json").write_text(json.dumps(out, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    (directory / "DbLanguage.zh-CN.coverage.json").write_text(json.dumps(coverage, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print(json.dumps(coverage["sections"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
