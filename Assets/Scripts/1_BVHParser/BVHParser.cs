using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// 如果有多个脚本文件在不同的文件夹中拥有相同的类名，可以通过命名空间来区分彼此
namespace Assets.Scripts
{
    public class BVHParser
    {
        public int frames = 0;
        public float frameTime = 1f / 24f;
        public BVHBone root;
        private List<BVHBone> boneList;

        static private char[] charMap = null;
        private float[][] channels;
        private string bvhText;
        private int pos = 0;

        /******************************************
         * BVHBone类：初始化骨骼节点对象
         * *****************************************************/
        public class BVHBone
        {
            public string name;
            public List<BVHBone> children;
            public float offsetX, offsetY, offsetZ;
            public int[] channelOrder;
            public int channelNumber;
            public BVHChannel[] channels;

            private BVHParser bp;

            // BVH Channel Order
            // 0 = Xpos, 1 = Ypos, 2 = Zpos, 3 = Xrot, 4 = Yrot, 5 = Zrot
            public struct BVHChannel
            {
                public bool enabled;
                public float[] values;
            }

            // BVHBone类的构造函数：解析BVH文件中的骨骼节点信息
            // 包括节点名称、偏移量、通道数量和子节点等，并根据解析结果构建骨骼层次结构。
            public BVHBone(BVHParser parser, bool rootBone)
            {
                bp = parser;
                // 将当前正在构造的 BVHBone 对象添加到 BVHParser 类的 boneList 列表中
                bp.boneList.Add(this);
                channels = new BVHChannel[6];
                channelOrder = new int[6] { 0, 1, 2, 5, 3, 4 };
                // 初始化当前节点的子节点列表
                children = new List<BVHBone>();

                bp.skip();
                if (rootBone)
                {
                    bp.assureExpect("ROOT");
                }
                else
                {
                    bp.assureExpect("JOINT");
                }
                // 解析并获取骨骼节点的名称
                bp.assure("joint name", bp.getString(out name));
                bp.skip();
                bp.assureExpect("{");
                bp.skip();
                bp.assureExpect("OFFSET");
                bp.skip();
                // 解析并获取偏移量
                bp.assure("offset X", bp.getFloat(out offsetX));
                bp.skip();
                bp.assure("offset Y", bp.getFloat(out offsetY));
                bp.skip();
                bp.assure("offset Z", bp.getFloat(out offsetZ));
                bp.skip();
                bp.assureExpect("CHANNELS");

                bp.skip();
                // 解析并获取节点的通道数量，且确保通道数量在有效范围内
                bp.assure("channel number", bp.getInt(out channelNumber));
                bp.assure("valid channel number", channelNumber >= 1 && channelNumber <= 6);

                // 将通道ID存入通道顺序数组，并将对应通道的状态设置为启用
                for (int i = 0; i < channelNumber; i++)
                {
                    bp.skip();
                    int channelId;
                    bp.assure("channel ID", bp.getChannel(out channelId));
                    channelOrder[i] = channelId;
                    channels[channelId].enabled = true;
                }

                // 循环解析节点的子节点或者结束标记
                char peek = ' ';
                do
                {
                    float ignored;
                    bp.skip();
                    // 获取下一个标记，用于判断子节点类型
                    bp.assure("child joint", bp.peek(out peek));
                    switch (peek)
                    {
                        case 'J':
                            BVHBone child = new BVHBone(bp, false);
                            children.Add(child);
                            break;
                        case 'E':
                            bp.assureExpect("End Site");
                            bp.skip();
                            bp.assureExpect("{");
                            bp.skip();
                            bp.assureExpect("OFFSET");
                            bp.skip();
                            bp.assure("end site offset X", bp.getFloat(out ignored));
                            bp.skip();
                            bp.assure("end site offset Y", bp.getFloat(out ignored));
                            bp.skip();
                            bp.assure("end site offset Z", bp.getFloat(out ignored));
                            bp.skip();
                            bp.assureExpect("}");
                            break;
                        case '}':
                            bp.assureExpect("}");
                            break;
                        default:
                            bp.assure("child joint", false);
                            break;
                    }
                } while (peek != '}');
            }
        }

        // 获取当前解析位置的字符
        private bool peek(out char c)
        {
            c = ' ';
            if (pos >= bvhText.Length)
            {
                return false;
            }
            c = bvhText[pos];
            return true;
        }

        // 检查在当前解析位置 pos 处是否存在与指定文本 text 相匹配的字符序列
        private bool expect(string text)
        {
            foreach (char c in text)
            {
                if (pos >= bvhText.Length || (c != bvhText[pos] && bvhText[pos] < 256 && c != charMap[bvhText[pos]]))
                {
                    return false;
                }
                pos++;
            }
            return true;
        }

        // 从 bvhText 字符串中获取一行文本并存储在 text 变量中
        // 然后去除两端的空白字符，最后返回是否成功获取到了非空的文本内容
        private bool getString(out string text)
        {
            text = "";
            // 逐个字符地获取一行文本
            while (pos < bvhText.Length && bvhText[pos] != '\n' && bvhText[pos] != '\r')
            {
                // 将当前位置 pos 对应的字符追加到 text 变量末尾
                // 并将 pos 向后移动一位，以便下一次循环获取下一个字符
                text += bvhText[pos++];
            }
            // 在获取完一行文本后，去除文本两端的空白字符(包括空格、制表符等)，确保文本内容的纯净性
            text = text.Trim();

            return (text.Length != 0);
        }

        // 从 bvhText 字符串中获取通道的索引，并根据通道名称判断其类型
        // 返回是否成功获取到了通道信息
        private bool getChannel(out int channel)
        {
            channel = -1;
            if (pos + 1 >= bvhText.Length)
            {
                return false;
            }
            switch (bvhText[pos])
            {
                case 'x':
                case 'X':
                    channel = 0;
                    break;
                case 'y':
                case 'Y':
                    channel = 1;
                    break;
                case 'z':
                case 'Z':
                    channel = 2;
                    break;
                default:
                    return false;
            }
            pos++;
            switch (bvhText[pos])
            {
                case 'p':
                case 'P':
                    pos++;      // 将解析位置向后移动一位
                    return expect("osition");      // 检查后续字符是否与"position"匹配
                case 'r':
                case 'R':
                    pos++;      // 将解析位置向后移动一位
                    channel += 3;       // 将通道索引加 3，表示旋转通道
                    return expect("otation");      // 检查后续字符是否与"rotation"匹配
                default:
                    return false;       // 无法识别通道类型
            }
        }

        // 从 bvhText 字符串中获取一个整数
        private bool getInt(out int v)
        {
            bool negate = false;    // 是否存在负号
            bool digitFound = false;    // 是否找到数字
            v = 0;

            // Read sign
            // 检查当前解析位置 pos 是否在字符串范围内，并且当前字符是否为负号或正号
            if (pos < bvhText.Length && bvhText[pos] == '-')
            {
                // 负号，则将 negate 置为 true，并将解析位置向后移动一位
                negate = true;
                pos++;
            }
            else if (pos < bvhText.Length && bvhText[pos] == '+')
            {
                // 正号，则只将解析位置向后移动一位
                pos++;
            }

            // Read digits
            // 循环检查当前解析位置 pos 是否在字符串范围内，并且当前字符是否为数字
            while (pos < bvhText.Length && bvhText[pos] >= '0' && bvhText[pos] <= '9')
            {
                // 将当前字符转换为数字并累加到整数变量 v 上，然后将解析位置向后移动一位
                v = v * 10 + (int)(bvhText[pos++] - '0');
                digitFound = true;
            }

            // Finalize
            if (negate)
            {
                v *= -1;
            }
            if (!digitFound)
            {
                v = -1;
            }
            // 返回一个布尔值，表示是否成功获取到了整数
            return digitFound;
        }

        // Accuracy looks okay
        // 从 bvhText 字符串中获取一个浮点数
        private bool getFloat(out float v)
        {
            bool negate = false;
            bool digitFound = false;
            int i = 0;
            v = 0f;            
            // Read sign
            if (pos < bvhText.Length && bvhText[pos] == '-')
            {
                negate = true;
                pos++;
            }
            else if (pos < bvhText.Length && bvhText[pos] == '+')
            {
                pos++;
            }
            // Read digits before decimal point
            // 读取小数点前的数字
            // 循环检查当前解析位置 pos 是否在字符串范围内，并且当前字符是否为数字
            while (pos < bvhText.Length && bvhText[pos] >= '0' && bvhText[pos] <= '9')
            {
                // 在循环中，将当前字符转换为数字并累加到浮点数变量v上，然后将解析位置向后移动一位
                v = v * 10 + (float)(bvhText[pos++] - '0');
                // 当前字符为数字，则将digitFound设置为true
                digitFound = true;
            }

            // Read decimal point
            // 检查当前解析位置 pos 是否在字符串范围内，并且当前字符是否为小数点或逗号
            if (pos < bvhText.Length && (bvhText[pos] == '.' || bvhText[pos] == ','))
            {
                pos++;
                // Read digits after decimal
                // 定义一个浮点数变量 fac，表示小数点后的位数
                float fac = 0.1f;
                // 循环检查当前解析位置 pos 是否在字符串范围内，并且当前字符是否为数字
                // 同时限制小数点后的位数最多为 128 位
                while (pos < bvhText.Length && bvhText[pos] >= '0' && bvhText[pos] <= '9' && i < 128)
                {
                    // 将小数点后的数字转换为浮点数并累加到浮点数变量 v 上，并将解析位置向后移动一位
                    v += fac * (float)(bvhText[pos++] - '0');
                    fac *= 0.1f;        // 更新小数点后的位数
                    digitFound = true;
                }
            }

            // Finalize
            if (negate)
            {
                v *= -1f;
            }

            // 检查当前解析位置 pos 是否在字符串范围内，并且当前字符是否为指数标识符 e
            if (pos < bvhText.Length && bvhText[pos] == 'e')
            {
                string scienceNum = "10";
                // 检查当前解析位置 pos 是否在字符串范围内，并确保当前字符非空格、制表符、换行符或回车符
                while (pos < bvhText.Length && bvhText[pos] != ' ' && bvhText[pos] != '\t' && bvhText[pos] != '\n' && bvhText[pos] != '\r')
                {
                    // 将指数部分的数字字符添加到 scienceNum 中，并将解析位置向后移动一位
                    scienceNum = scienceNum + bvhText[pos];
                    pos++;
                }
                // 将科学计数法表示的指数部分转换为浮点数，并乘以原浮点数变量 v
                v = v * (float)Double.Parse(scienceNum);
            }
            // 如果未找到数字，将浮点数变量 v 设置为 NaN。
            if (!digitFound)
            {
                v = float.NaN;
            }
            // 返回一个布尔值，表示是否成功获取到了浮点数
            return digitFound;
        }

        // 跳过当前位置 pos 后面的所有空白字符，包括空格' '、制表符'\t'、换行符'\n' 和回车符'\r'
        private void skip()
        {
            while (pos < bvhText.Length && (bvhText[pos] == ' ' || bvhText[pos] == '\t' || bvhText[pos] == '\n' || bvhText[pos] == '\r'))
            {
                pos++;
            }
        }

        // 跳过当前位置 pos 后面的所有行内空白字符，包括空格' ' 和制表符'\t'
        private void skipInLine()
        {
            while (pos < bvhText.Length && (bvhText[pos] == ' ' || bvhText[pos] == '\t'))
            {
                pos++;
            }
        }

        // 跳过当前位置 pos 后面的所有换行符，并确保至少找到一个换行符
        private void newline()
        {
            bool foundNewline = false;  // 标记是否找到了换行符
            skipInLine();   // 跳过当前位置后的行内空白字符
            while (pos < bvhText.Length && (bvhText[pos] == '\n' || bvhText[pos] == '\r'))
            {
                foundNewline = true;
                pos++;
            }
            // 如果没有找到换行符，则抛出异常，指示在当前位置未找到换行符
            assure("newline", foundNewline);
        }

        // 确保某个条件为真
        // 如果条件不满足，则抛出异常，指示解析 BVH 数据失败，并提供失败位置附近的文本
        // what 是描述期望条件的字符串，result 是表示条件是否满足的布尔值。
        private void assure(string what, bool result)
        {
            if (!result)
            {
                string errorRegion = "";
                for (int i = Math.Max(0, pos - 15); i < Math.Min(bvhText.Length, pos + 15); i++)
                {
                    if (i == pos - 1)
                    {
                        errorRegion += ">>>";
                    }
                    errorRegion += bvhText[i];
                    if (i == pos + 1)
                    {
                        errorRegion += "<<<";
                    }
                }
                throw new ArgumentException("Failed to parse BVH data at position " + pos + ". Expected " + what + " around here: " + errorRegion);
            }
        }

        // 确保当前位置的文本与给定的 text 相匹配
        // text 表示期望的文本内容，expect(text) 表示当前位置的文本是否与给定的 text 相匹配
        private void assureExpect(string text)
        {
            assure(text, expect(text));
        }

        /*private void tryCustomFloats(string[] floats) {
            float total = 0f;
            foreach (string f in floats) {
                pos = 0;
                bvhText = f;
                float v;
                getFloat(out v);
                total += v;
            }
            Debug.Log("Custom: " + total);
        }

        private void tryStandardFloats(string[] floats) {
            IFormatProvider fp = CultureInfo.InvariantCulture;
            float total = 0f;
            foreach (string f in floats) {
                float v = float.Parse(f, fp);
                total += v;
            }
            Debug.Log("Standard: " + total);
        }

        private void tryCustomInts(string[] ints) {
            int total = 0;
            foreach (string i in ints) {
                pos = 0;
                bvhText = i;
                int v;
                getInt(out v);
                total += v;
            }
            Debug.Log("Custom: " + total);
        }

        private void tryStandardInts(string[] ints) {
            IFormatProvider fp = CultureInfo.InvariantCulture;
            int total = 0;
            foreach (string i in ints) {
                int v = int.Parse(i, fp);
                total += v;
            }
            Debug.Log("Standard: " + total);
        }

        public void benchmark () {
            string[] floats = new string[105018];
            string[] ints = new string[105018];
            for (int i = 0; i < floats.Length; i++) {
                floats[i] = UnityEngine.Random.Range(-180f, 180f).ToString();
            }
            for (int i = 0; i < ints.Length; i++) {
                ints[i] = ((int)Mathf.Round(UnityEngine.Random.Range(-180f, 18000f))).ToString();
            }
            tryCustomFloats(floats);
            tryStandardFloats(floats);
            tryCustomInts(ints);
            tryStandardInts(ints);
        }*/

        /*****************************************************************************************
         * 解析BVH格式的动画数据,包括骨骼层级结构和帧数据，最终将解析结果存储在适当的数据结构中
         * 布尔值 overrideFrameTime 用于确定是否应该覆盖帧时间
         * 浮点数 time 用于覆盖帧时间的实际值
         * 
         * 解析存储结构
         * BVHBone 对象：每个骨骼都表示为一个BVHBone对象，每个BVHBone对象包含骨骼的名称、偏移量、通道信息等
         * float帧数组： 用于存储动画的帧数据，这些数组存储了每个通道在每一帧的值
         *              通道的数量由所有骨骼的通道数之和决定，每个通道的值通过一个一维浮点数组来表示
         * List<BVHBone> 对象：BVHBone对象通过骨骼层级结构相互连接，构成了骨骼的层次结构
         * 
         * parse方法：
         * 创建根 BVHBone 对象
         * 通过递归的方式解析每个骨骼及其通道信息，并将其添加到 BVHBone 对象的列表中
         * 根据动画的帧数和每帧的时间，创建了用于存储帧数据的浮点数组
         * 通过循环遍历每一帧和每个通道，将解析得到的帧数据存储在相应的位置上
         * **********************************************************/
        private void parse(bool overrideFrameTime, float time)
        {
            // Prepare character table
            // 字符映射表：将字符转换为大写形式。
            if (charMap == null)
            {
                // 创建字符映射表，大小为256，每个字符对应一个ASCII码值
                charMap = new char[256];
                for (int i = 0; i < 256; i++)   // 开始循环遍历ASCII码值0到255
                {
                    if (i >= 'a' && i <= 'z')   // 检查当前ASCII码是否在小写字母范围内
                    {
                        // 如果是小写字母，则将其转换为大写字母
                        charMap[i] = (char)(i - 'a' + 'A');
                    }
                    // 检查当前ASCII码是否是制表符、换行符或回车符
                    else if (i == '\t' || i == '\n' || i == '\r')
                    {
                        charMap[i] = ' ';   //  如果是以上特殊字符，则将其替换为空格
                    }
                    else
                    {
                        charMap[i] = (char)i;   // 如果以上条件都不满足，则保留原始字符
                    }
                }
            }

            // Parse skeleton 解析骨骼
            // 确保下一个标记是 "HIERARCHY"
            skip();
            assureExpect("HIERARCHY");

            boneList = new List<BVHBone>();
            root = new BVHBone(this, true);

            // Parse meta data
            skip();
            assureExpect("MOTION");
            skip();
            assureExpect("FRAMES:");
            skip();
            // 解析帧数，并将结果存储在 frames 变量中
            assure("frame number", getInt(out frames));
            skip();
            assureExpect("FRAME TIME:");
            skip();
            // 解析帧时间，并将结果存储在 frameTime 变量中
            assure("frame time", getFloat(out frameTime));

            if (overrideFrameTime)
            {
                frameTime = time;   // 如果 overrideFrameTime 为真，则将 time 赋值给 frameTime，以覆盖默认帧时间
            }

            // Prepare channels
            // totalChannels 存储所有通道的总数
            int totalChannels = 0;
            foreach (BVHBone bone in boneList)
            {
                // 将当前骨骼的通道数累加到 totalChannels 变量中
                totalChannels += bone.channelNumber;
            }
            // 整数变量 channel，追踪当前通道的索引
            int channel = 0;
            channels = new float[totalChannels][];
            foreach (BVHBone bone in boneList)
            {
                for (int i = 0; i < bone.channelNumber; i++)
                {
                    // 为当前通道创建一个大小为 frames 的浮点数组
                    channels[channel] = new float[frames];
                    // 将当前通道的值数组分配给骨骼中对应通道的值
                    bone.channels[bone.channelOrder[i]].values = channels[channel++];
                }
            }

            // Parse frames
            // 循环遍历每一帧
            for (int i = 0; i < frames; i++)
            {
                newline();
                for (channel = 0; channel < totalChannels; channel++)
                {                    
                    skipInLine();   // 跳过行内的空格和制表符
                    // 解析当前通道的值，并将其存储在 channels 数组中的适当位置。
                    assure("channel value", getFloat(out channels[channel][i]));
                }
            }
        }

        // BVHParser 类的构造函数，用于创建 BVHParser 对象
        // 按照默认帧时间解析动画数据
        public BVHParser(string bvhText)
        {
            this.bvhText = bvhText;

            parse(false, 0f);
        }

        // 接受一个字符串参数 bvhText，并且额外接受一个浮点数参数 time
        // 此构造函数允许指定帧时间，会按照指定的帧时间解析动画数据
        public BVHParser(string bvhText, float time)
        {
            this.bvhText = bvhText;

            parse(true, time);
        }

        // 将欧拉角转换为四元数
        private Quaternion eul2quat(float z, float y, float x)
        {
            // 将输入的欧拉角转换为弧度制，因为 Mathf.Cos 和 Mathf.Sin 函数使用弧度而不是角度作为输入
            z = z * Mathf.Deg2Rad;
            y = y * Mathf.Deg2Rad;
            x = x * Mathf.Deg2Rad;

            // 定义了两个数组 c 和 s，分别用来存储每个角度的余弦值和正弦值
            // 动捕数据是ZYX，但是unity是ZXY
            float[] c = new float[3];
            float[] s = new float[3];
            c[0] = Mathf.Cos(x / 2.0f); c[1] = Mathf.Cos(y / 2.0f); c[2] = Mathf.Cos(z / 2.0f);
            s[0] = Mathf.Sin(x / 2.0f); s[1] = Mathf.Sin(y / 2.0f); s[2] = Mathf.Sin(z / 2.0f);

            // 根据转换公式将计算得到的余弦和正弦值组合成四元数
            // Unity 中默认使用 ZXY 欧拉角顺序，所以转换公式也是基于 ZXY 欧拉角顺序的
            return new Quaternion(
                c[0] * c[1] * s[2] - s[0] * s[1] * c[2],
                c[0] * s[1] * c[2] + s[0] * c[1] * s[2],
                s[0] * c[1] * c[2] - c[0] * s[1] * s[2],
                c[0] * c[1] * c[2] + s[0] * s[1] * s[2]
                );
        }

        // 获取骨骼的层级关系，并将其以字典的形式返回，其中每个键值对表示子骨骼和其对应的父骨骼
        public Dictionary<string,string> getHierachy()
        {
            Dictionary<string, string> hierachy = new Dictionary<string, string>();
            foreach (BVHBone bb in boneList)    // 遍历存储所有骨骼的列表 boneList 中的每一个骨骼
            {
                // 对于当前骨骼 bb，遍历其子骨骼列表 children 中的每一个子骨骼 bbc
                foreach (BVHBone bbc in bb.children)    
                {
                    // 建立父子骨骼关系
                    // 将当前子骨骼 bbc 的名称作为键，其父骨骼 bb 的名称作为值，添加到字典 hierarchy 中
                    hierachy.Add(bbc.name, bb.name);
                }
            }
            // 返回包含骨骼层级关系的数组
            return hierachy;
        }

        // 获取特定帧的关键帧数据，包括每个骨骼节点的旋转信息以及根节点的位移信息
        public Dictionary<string,Quaternion> getKeyFrame(int frameIdx)
        {
            // 获取骨骼的层级关系，包含每个骨骼节点及其父节点的映射关系
            Dictionary<string, string> hierachy = getHierachy();
            // 定义一个四元数数组，用于存储关键帧数据，其中键为骨骼名称，值为该骨骼节点的旋转信息
            Dictionary<string, Quaternion> boneData = new Dictionary<string, Quaternion>();
            // 记录根节点位移更新，键为pos，帧为由当前帧的位移数据构成的四元数    
            boneData.Add("pos", new Quaternion(
                boneList[0].channels[0].values[frameIdx],
                boneList[0].channels[1].values[frameIdx],
                boneList[0].channels[2].values[frameIdx],0));
            // 以四元数的形式，记录根节点旋转更新，键为根节点的名称，值为通过欧拉角转换为四元数的根节点旋转信息
            boneData.Add(boneList[0].name, eul2quat(
                    boneList[0].channels[3].values[frameIdx],
                    boneList[0].channels[4].values[frameIdx],
                    boneList[0].channels[5].values[frameIdx]));
            // 以四元数的形式，记录每个骨骼关节点相对父节点的旋转
            foreach (BVHBone bb in boneList)
            {
                if (bb.name != boneList[0].name)    // 确保当前骨骼不是根节点
                {
                    // 获得该骨骼关节点的local rotation，以四元数的形式记录旋转矩阵
                    Quaternion localrot = eul2quat(bb.channels[3].values[frameIdx],
                        bb.channels[4].values[frameIdx],
                        bb.channels[5].values[frameIdx]);
                    // 更新boneData中的旋转信息，将相应位置的原本四元数旋转信息与新的旋转信息相乘，得到此时相对父节点的旋转
                    boneData.Add(bb.name, boneData[hierachy[bb.name]] * localrot);
                }
            }            
            return boneData;
        }
        /*
         boneData的数据格式：
            [0] [pos, Quaternion(x, y, z, 0)]
            [1] [boneName_Hips, Quaternion.HipsRotationInThisFrame]
            [2] [boneNames_LHipJoint, Quaternion.LHipJointRotationInThisFrame]
            ...
            [31][boneNames_RThumb, Quaternion.RThumbRotationInThisFrame]
         */

        // 获取每个骨骼的偏移量，即每个骨骼相对父节点的偏移量
        // 函数接受一个浮点数参数 ratio，用于缩放偏移量
        public Dictionary<string,Vector3> getOffset(float ratio) {
            Dictionary<string, Vector3> offset = new Dictionary<string, Vector3>();
            foreach(BVHBone bb in boneList)
            {
                // 键为骨骼名称，值为根据给定比例缩放后的偏移量
                offset.Add(bb.name, new Vector3(bb.offsetX * ratio, bb.offsetY * ratio, bb.offsetZ * ratio));
            }
            return offset;
        }
    }
}
