# 🧀 芝士就是力量 (Cheese is Power)

![Cheese Icon](./assets/cheese.png) <!-- 请把芝士图标放在assets文件夹下，并把这里换成实际路径 -->

**“知识就是力量？不，芝士才是。”**
*"Knowledge is power? No, Cheese is power."*

一款由初中极客使用 **C# 和 WinForms** 原生手搓的极简硬核塔防游戏。没有花里胡哨的 3D 建模，没有逼氪的内购，只有最纯粹的塔防策略，以及香喷喷的芝士。
*A minimalist, hardcore Tower Defense game built natively with **C# and WinForms** by a junior high school geek. No fancy 3D graphics, no microtransactions—just pure strategy and delicious cheese.*

---

## 🎮 游戏玩法 (How to Play)

怪物（👿）会从左侧的红点源源不断地涌出，目标是吃掉右侧的玩家本体（绿点）。
*   **玩家血量**：3 点。怪物碰到玩家后消失，玩家扣 1 点血。血量归零则 Game Over。
*   **核心资源**：**芝士（🧀）**。初始拥有 50 点芝士，芝士就是你的防御资金！

*Monsters (👿) spawn from the left red point and march toward the player (green point).*
*   **Player HP**: 3. If a monster reaches the base, it dies and the player loses 1 HP. Reach 0 and it's Game Over.*
*   **Core Resource**: **Cheese (🧀)**. Start with 50 Cheese. Cheese is your defense fund!*

---

## ✨ 核心机制 (Core Mechanics)

### 🧀 芝士经济学 (The Cheese Economy)
*   **芝士农民 (🧔 Farmer)**：消耗 50 芝士放置在墙上。每 10 秒产出一波，**有 50% 概率产出 25 点芝士**（纯看脸，无保底！）。
*   *Cost 50 Cheese, placed on walls. Every 10 seconds, there's a **50% chance** to produce 25 Cheese. (RNG-based, no pity system!)*

### 🛡️ 防御体系 (Defense System)
*   **警察 (👮‍♂️ Cop)**：消耗 100 芝士。基础单发炮塔。
    *(Cost 100 Cheese. Basic single-shot turret.)*
*   **卫兵 (💂‍♂️ Guard)**：消耗 175 芝士。一次发射 2 颗子弹，火力压制！
    *(Cost 175 Cheese. Fires double bullets for heavy suppression.)*
*   **停止路障 (✋ Stop Sign)**：消耗 30 芝士，放置在地面。拥有 25 点耐久度。怪物遇到它必须停下，每秒啃咬一次。**这是你把怪物卡在警察火力圈内的绝佳战术！**
    *(Cost 30 Cheese, placed on the ground. Has 25 HP. Monsters will stop and attack it. Use this to stall enemies inside your turrets' kill zone!)*

### 🧭 寻路系统 (Pathfinding)
*   怪物极其聪明，它们使用了 **A* (A-Star) 寻路算法**，会自动绕过墙体和你布置的障碍物，寻找通往玩家的最短路径。
*   *Monsters are smart. They use the **A* (A-Star) algorithm** to dynamically navigate around walls and obstacles to reach the player.*

---

## 📸 游戏截图 (Screenshots)

<img width="1172" height="500" alt="图片" src="https://github.com/user-attachments/assets/71410685-a0d7-48e1-88cd-6a42f339fcde" />


---

## 🛠️ 技术栈 (Tech Stack)

*   **Language**: C# (.NET)
*   **UI Framework**: WinForms (GDI+ 绘制, 开启双缓冲防闪烁)
*   **Algorithm**: A* 寻路算法 (A-Star Pathfinding)

---

## 📥 下载与安装 (Download & Run)

请前往右侧的 **[Releases](你的Release链接)** 页面，下载最新版本的 `.7z` 压缩包。

> ⚠️ **注意事项 (Must Read!)**
> 1. **运行环境**：请确保你的 Windows 系统已安装 .NET Desktop Runtime。
> 2. **杀软误报**：由于个人开发者编译无商业签名，Windows Defender 可能报毒，请放心点击“仍要运行”。
> 3. **解压运行**：请使用 7-Zip 解压后，双击 `CheeseIsPower.exe` 即可游玩。
>
> *1. **Runtime**: Requires .NET Desktop Runtime on Windows.*
> *2. **Antivirus False Positives**: Built by an independent dev without a commercial code-signing certificate. Please allow it if flagged.*
> *3. **How to Play**: Download the `.7z` package, extract it with 7-Zip, and run `CheeseIsPower.exe`.*

---

## 💡 开发者的话 (Developer's Note)

如果你觉得这个游戏有意思，或者帮你打发了无聊的时间，欢迎给本项目点一个 ⭐ **Star**！
你的支持是我继续用代码和芝士改变世界的最大动力。

*If you find this game interesting, please consider giving it a ⭐ **Star**!*
*Your support is my biggest motivation to keep changing the world with code and cheese.*

---
*Built with ❤️ and 🧀 by YumPepper.*
