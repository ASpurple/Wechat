using System;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Web;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Web.UI.WebControls;

namespace PhraseApp
{

    class main
    {
        public static void Main(string[] args)
        {
            Message msg = new Message();
            msg.getUUID();
            msg.getScan();
            msg.waitForLogin();
            msg.setTicket(msg.waitForLogin());
            msg.setSWWP(msg.getCookie());
            msg.Init();
            msg.getMessage();
        }
    }


    //定义工具类，负责字符编码解码，发送 POST 和 GET 请求
    static class Tools
    {

        //生成当前时间戳
        public static long GetCurrentTimeUnix()
        {
            //TimeSpan cha = (DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)));
            //long t = (long)cha.TotalSeconds;
            return (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
        }

        //验证是否是中文
        public static bool isChinese(string msg)
        {
            string temp;
            for (int i = 0; i < msg.Length; i++)
            {
                temp = msg.Substring(i, 1);
                byte[] ser = Encoding.GetEncoding("gb2312").GetBytes(temp);
                if (ser.Length == 2)
                {
                    return true;
                }
            }
            return false;
        }

        //GET方法获取数据
        public static string getRes(string url, string refe)
        {
            string str = "";
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "GET";
                request.UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.94 Safari/537.36";
                request.Referer = refe;
                using (HttpWebResponse wr = (HttpWebResponse)request.GetResponse())
                {
                    Stream respStream = wr.GetResponseStream();
                    StreamReader respStreamReader = new StreamReader(respStream, Encoding.UTF8);
                    str = respStreamReader.ReadToEnd();
                    str = str.Replace(" ", "");

                    /* 					using (StreamWriter sw = new StreamWriter("res.html")){
                                            sw.WriteLine(str);
                                        } */
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return str;
        }

        //POST方法获取数据
        public static string postRes(string url, string para)
        {
            string str = "";
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "POST";
                request.UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.94 Safari/537.36";
                request.Referer = url;
                byte[] data = Encoding.Default.GetBytes(para);
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                Stream newStream = request.GetRequestStream();
                newStream.Write(data, 0, data.Length);
                newStream.Close();
                using (HttpWebResponse wr = (HttpWebResponse)request.GetResponse())
                {
                    Stream respStream = wr.GetResponseStream();
                    StreamReader respStreamReader = new StreamReader(respStream, Encoding.UTF8);
                    str = respStreamReader.ReadToEnd();
                    str = str.Replace(" ", "");

                    /* 					using (StreamWriter sw = new StreamWriter("res.html")){
                                            sw.WriteLine(str);
                                        } */
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return str;
        }

        //字符编码
        public static string enCoding(string word)
        {
            return System.Web.HttpUtility.UrlEncode(word, System.Text.Encoding.UTF8);
        }

        //字符串转UNICODE
        public static string String2Unicode(string source)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(source);
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i += 2)
            {
                stringBuilder.AppendFormat("{0}{1}", bytes[i + 1].ToString("x").PadLeft(2, '0'), bytes[i].ToString("x").PadLeft(2, '0'));
            }
            return stringBuilder.ToString();
        }

    }

    //定义消息处理类，接收，处理，发送消息
    class Message
    {

        //cookie
        CookieContainer cookie = new CookieContainer();

        //获取微信登陆时的UUID
        private string uuid = "";
        public void getUUID() {
            string url = @"https://login.wx.qq.com/jslogin?appid=wx782c26e4c19acffb&redirect_uri=https%3A%2F%2Fwx.qq.com%2Fcgi-bin%2Fmmwebwx-bin%2Fwebwxnewloginpage&fun=new&lang=zh_CN&_=" + Tools.GetCurrentTimeUnix().ToString();
            string r = getRes(url, @"https://wx.qq.com/");
            r = r.Substring(r.IndexOf("uuid")+6, r.IndexOf("\"", r.IndexOf("uuid") + 6) - r.IndexOf("uuid") - 6);
            uuid = r;
        }

        //获取并显示二维码
        public void getScan() {
            string url = @"https://login.weixin.qq.com/qrcode/" + uuid;
            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();
            Stream reader = response.GetResponseStream();
            FileStream writer = new FileStream("wechatpic.jpg", FileMode.OpenOrCreate, FileAccess.Write);
            byte[] buff = new byte[512];
            int c = 0; //实际读取的字节数
            while ((c = reader.Read(buff, 0, buff.Length)) > 0)
            {
                writer.Write(buff, 0, c);
            }
            writer.Close();
            writer.Dispose();
            reader.Close();
            reader.Dispose();
            response.Close();
            //调用系统图片查看器显示二维码
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "wechatpic.jpg";
            process.StartInfo.Arguments = "rundll32.exe C://WINDOWS//system32//shimgvw.dll,ImageView_Fullscreen";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
            process.Start();
            process.Dispose();
        }

        //等待登陆（心跳包轮询）
        public string waitForLogin() {
            string r = "408";
            long l;
            string time;
            int tip = 1;
            string url;
            do {
                l = Tools.GetCurrentTimeUnix();
                time = (~l).ToString();
                url = "https://login.wx.qq.com/cgi-bin/mmwebwx-bin/login?loginicon=true&uuid=" + uuid + "&tip=" + tip.ToString() + "&r=" + time + "&_=" + l.ToString();
                r = getRes(url, "https://wx.qq.com/");
                tip = 0;
                if (r.IndexOf("201") != -1) tip = 1;
            }while(r.IndexOf("window.code=200") == -1);
            return r;
        }

        string ticket;
        string scan;
        //从登陆后的返回值中提取 ticket，scan 等信息
        public void setTicket(string logMsg) {
            ticket = logMsg.Substring(logMsg.IndexOf("ticket=")+7,logMsg.IndexOf("&", logMsg.IndexOf("ticket=") + 7)- logMsg.IndexOf("ticket=") - 7);
            uuid = logMsg.Substring(logMsg.IndexOf("uuid=") + 5, logMsg.IndexOf("&", logMsg.IndexOf("uuid=") + 5) - logMsg.IndexOf("uuid=") - 5);
            scan = logMsg.Substring(logMsg.IndexOf("scan=") + 5, logMsg.IndexOf(";", logMsg.IndexOf("scan=")) - logMsg.IndexOf("scan=") - 6);
        }

        public void test() {
            Console.WriteLine(uuid);
            Console.WriteLine(ticket);
            Console.WriteLine(scan);
            Console.WriteLine(skey);
            Console.WriteLine(wxsid);
            Console.WriteLine(wxuin);
            Console.WriteLine(pass_ticket);
            Console.WriteLine(synckey);

        }

        //登陆获取Cookie等重要信息
        public string getCookie() {
            string url = "https://wx2.qq.com/cgi-bin/mmwebwx-bin/webwxnewloginpage?ticket="+ ticket +"&uuid="+ uuid +"&lang=zh_CN&scan="+ scan +"&fun=new&version=v2";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Referer = @"https://wx2.qq.com/";
            request.CookieContainer = cookie;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader respStreamReader = new StreamReader(myResponseStream, Encoding.UTF8);
            string str = respStreamReader.ReadToEnd();
            return str;
        }

        private string skey;
        private string wxsid;
        private string wxuin;
        private string pass_ticket;
        private string synckey = "";

        //从获取cookie之后的返回值中提取：skey，wxsid，wxuin，pass_ticket
        public void setSWWP(string cookieStr) {
			try{
			    skey = cookieStr.Substring(cookieStr.IndexOf("<skey>")+6, cookieStr.IndexOf("</skey>") - cookieStr.IndexOf("<skey>") - 6);
				wxsid = cookieStr.Substring(cookieStr.IndexOf("<wxsid>") + 7, cookieStr.IndexOf("</wxsid>") - cookieStr.IndexOf("<wxsid>") - 7);
				wxuin = cookieStr.Substring(cookieStr.IndexOf("<wxuin>") + 7, cookieStr.IndexOf("</wxuin>") - cookieStr.IndexOf("<wxuin>") - 7);
				pass_ticket = cookieStr.Substring(cookieStr.IndexOf("<pass_ticket>") + 13, cookieStr.IndexOf("</pass_ticket>") - cookieStr.IndexOf("<pass_ticket>") - 13);

			}catch(Exception e){
				Console.WriteLine(e);
			}
 
        }

//string url = "https://wx2.qq.com/cgi-bin/mmwebwx-bin/webwxsync?sid="+ wxsid +"&skey="+ skey +"&pass_ticket=" + pass_ticket;

        long num = Tools.GetCurrentTimeUnix();
        string dataModel = "{\"BaseRequest\":{\"Uin\":$uin$,\"Sid\":\"$sid$\",\"Skey\":\"$skey$\",\"DeviceID\":\"$device$\"}}";
        string sck = "";
        string user = "";
        //微信登录之后初始化 + 提取 synckey 和 UserName
        public void Init() {
            string url = "https://wx2.qq.com/cgi-bin/mmwebwx-bin/webwxinit?r=" + Tools.GetCurrentTimeUnix().ToString() + "&pass_ticket=" + pass_ticket;
            dataModel = dataModel.Replace("$uin$",wxuin);
            dataModel = dataModel.Replace("$sid$", wxsid);
            dataModel = dataModel.Replace("$skey$", skey);
            dataModel = dataModel.Replace("$device$", getDice());
            string r = post(url,dataModel, @"https://wx2.qq.com/");
            int uns = r.IndexOf("\"User\"")+10;
            uns = r.IndexOf("UserName",uns) + 11;
            user = r.Substring(uns,r.IndexOf(",",uns)-1-uns);
            int s = r.IndexOf("SyncKey") + 9;
            sck = r.Substring(s,r.IndexOf("\"User\"",s)-2-s);
			//第一次格式化 synckey
			try
            {
                r = sck;
				int sk = r.IndexOf("Key")-1;
                while (r.IndexOf("Key",sk) != -1) {
                    sk = r.IndexOf("Key",sk) + 5;
                    synckey += r.Substring(sk,r.IndexOf(",",sk)-sk);
                    synckey += "_";
                    sk = r.IndexOf("Val",sk)+5;
                    synckey += r.Substring(sk,r.IndexOf("}",sk)-sk-1);
                    synckey += "%7C";
                }
                synckey = synckey.Substring(0,synckey.Length-3);
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        //获取 SyncCheckKey 和新消息 并更新 sck 和 SyncCheckKey,每次收到新消息或者发送消息后需要进行此操作
        public string getSyncCheckKey(){
            string url = "https://wx2.qq.com/cgi-bin/mmwebwx-bin/webwxsync?sid="+ wxsid +"&skey="+ skey + "&lang=zh_CN" + "&pass_ticket=" + pass_ticket;
            string data = dataModel.Substring(0,dataModel.Length - 2) + "},\"SyncKey\":" + sck +",\"rr\":" + ~Tools.GetCurrentTimeUnix() +"}";
			string r = post(url,data,@"https://wx2.qq.com/");
            if(r.IndexOf("\"Ret\":0") != -1){
                int st = r.IndexOf("SyncCheckKey") + 14;
                sck = r.Substring(st);
                sck = sck.Substring(0,sck.Length-3);
                SetcheckKey(r);
            }
            return r;
        }
        //格式化 synccheckkey，供消息检查接口使用
        public void SetcheckKey(string r){
            synckey = "";
            try
            {
                int sk = r.IndexOf("SyncCheckKey") + 15;
                r = r.Substring(sk);
                sk = r.IndexOf("Key");
                while (r.IndexOf("Key",sk) != -1) {
                    sk = r.IndexOf("Key",sk) + 5;
                    synckey += r.Substring(sk,r.IndexOf(",",sk)-sk);
                    synckey += "_";
                    sk = r.IndexOf("Val",sk)+5;
                    synckey += r.Substring(sk,r.IndexOf("}",sk)-sk-1);
                    synckey += "%7C";
                }
                synckey = synckey.Substring(0,synckey.Length-3);
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        //生成 DevinceID 
        public string getDice() {
            Random ran = new Random();
            string device = "e";
            for (int i = 1; i < 16; i++)
            {
                device += ran.Next(10).ToString();
            }
            return device;
        }
                                                    
        //循环消息检查 + 获取消息
        public void getMessage()
        {
            while(true){
                num++;
			    string url = "https://webpush.wx2.qq.com/cgi-bin/mmwebwx-bin/synccheck?r="+ Tools.GetCurrentTimeUnix().ToString() +"&skey="+ Tools.enCoding(skey) +"&sid="+ Tools.enCoding(wxsid) +"&uin="+ wxuin +"&deviceid="+ getDice() +"&synckey=" + synckey + "&_=" + num.ToString();
                string r = getRes(url,@"https://wx2.qq.com/");
                Console.WriteLine("消息检查回复："+r);
                if (r.IndexOf("0", r.IndexOf("selector")) == -1)
                {
                    string res = getSyncCheckKey();
                    if(res.IndexOf("\"AddMsgList\":[]") ==  -1){
                        //消息处理函数
                        string msg = getMsgContent(res);
                        string from = getFrom(res);
                        switch(msg){
                            case "你最爱谁":
                            sendMsg("我最爱欧阳小饼","1",from);
                            break;
                        }
                        sendMsg(phraseGame(msg),"1",from);
                    }
                }
            }
        }

        //新消息处理函数
        public string getMsgContent(string res){
            int msgs = res.IndexOf("Content") + 10;
            int msge = res.IndexOf(",",msgs) - 1;
            return res.Substring(msgs,msge-msgs);
        }

        //获取发送人
        public string getFrom(string res){
            int fs = res.IndexOf("FromUserName") + 15;
            int fe = res.IndexOf(",",fs) - 1;
            return res.Substring(fs,fe-fs);
        }

        //发送消息
        public void sendMsg(string msg,string type,string to){
            string clientid = getClientId();
            string url = "https://wx2.qq.com/cgi-bin/mmwebwx-bin/webwxsendmsg?lang=zh_CN&pass_ticket=" + Tools.enCoding(pass_ticket);
            string data = "{\"BaseRequest\":{\"Uin\":"+ wxuin +",\"Sid\":\""+ wxsid +"\",\"Skey\":\""+ skey +"\",\"DeviceID\":\""+ getDice() +"\"},\"Msg\":{\"Type\":"+ type +",\"Content\":\""+ msg +"\",\"FromUserName\":\""+ user +"\",\"ToUserName\":\""+ to +"\",\"LocalID\":\""+ clientid +"\",\"ClientMsgId\":\""+ clientid +"\"},\"Scene\":0}";
            write(data);
            post(url,data,"https://wx2.qq.com/?&lang=zh_CN");
            getSyncCheckKey();
        }

        //生成 ClientMsgId 和 LocalID ，两者相等
        public string getClientId(){
            string s = "";
            Random ran = new Random();
            for(int i=0;i<4;i++){
                s += ran.Next(10).ToString();
            }
            return Tools.GetCurrentTimeUnix().ToString() + s;
        }

        //成语游戏
        public string phraseGame(string msg){
            Phrase ph = new Phrase();
            string key = "";
            if(msg.Length > 1 && msg.Substring(0,1) == "#"){
                key = msg.Substring(1,1);
            }else if(msg.Length > 4 && msg.Substring(0,1) == "【"){
                key = msg.Substring(1,4);
            }    
            return ph.searchPhrase(key);
        }

        //写文件
        public void write(string str){
            using(StreamWriter sw = new StreamWriter("Log.txt")){
                sw.WriteLine(str);
            }
        }

        //get方法发送请求获取消息
        public string getRes(string url, string referer)
        {
            string str = ""; //存储响应数据
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "GET";
                request.UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.94 Safari/537.36";
                request.Referer = referer;
                request.Accept = "*/*";
                request.Headers["AcceptEncoding"] = "gzip, deflate, br";
                request.Headers["AcceptLanguage"] = "zh-CN,zh;q=0.9";
                request.CookieContainer = cookie;

                using (HttpWebResponse wr = (HttpWebResponse)request.GetResponse())
                {
                    Stream respStream = wr.GetResponseStream();
                    StreamReader respStreamReader = new StreamReader(respStream, Encoding.UTF8);
                    str = respStreamReader.ReadToEnd();
                    str = str.Replace(" ", "");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("新建连接");
            }
            return str;
        }


        //POST方法发送请求,post参数需传入转码后的参数
        public string post(string url, string para, string referer)
        {
            string str = ""; //存储响应数据
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "POST";
                request.UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.94 Safari/537.36";
                request.Referer = referer;
                request.Accept = "application/json, text/plain, */*";
                request.Headers["AcceptEncoding"] = "gzip, deflate, br";
                request.Headers["AcceptLanguage"] = "zh-CN,zh;q=0.8,en;q=0.6";
                request.CookieContainer = cookie;
                request.Timeout = 8000;

                byte[] data = Encoding.UTF8.GetBytes(para);
                request.ContentType = "application/json;charset=UTF-8";
                request.ContentLength = data.Length;
                Stream newStream = request.GetRequestStream();
                newStream.Write(data, 0, data.Length);
                newStream.Close();
                using (HttpWebResponse wr = (HttpWebResponse)request.GetResponse())
                {
                    Stream respStream = wr.GetResponseStream();
                    StreamReader respStreamReader = new StreamReader(respStream, Encoding.UTF8);
                    str = respStreamReader.ReadToEnd();
                    str = str.Replace(" ", "");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("新建连接");
            }
            return str;
        }
    }




    //定义成语类,实例化后，先调用 setKey 方法设置查询关键词，再调用 searchPhrase 方法，返回接龙成语
    class Phrase
    {

        public string searchPhrase(string k)
        {
            setKey(k);
            string[] phrases = getFromHtml(getHtml());
            Random ran = new Random();
            int sub = ran.Next(phrases.Length);
            return phrases[sub];
        }

        //搜索关键词
        private string key = "";
        public void setKey(string k)
        {
            key = k;
        }

        //根据关键词从网络获取HTML文件(只对 https://chengyu.911cha.com/ 有效)
        public string getHtml()
        {
            string[] words = new string[] { "", "", "", "" };
            for (int i = 0; i < key.Length; i++)
            {
                words[i] = Tools.enCoding(key[i].ToString());
            }
            if (key.Length == 1)
            {
                key = @"zi1=" + words[0] + "&zi2=" + words[1] + "&zi3=" + words[2] + "&zi4=" + words[3] + "&sotype=1";
            }
            else
            {
                key = @"zi1=" + words[3] + "&zi2=&zi3=&zi4=&sotype=1";
            }

            string str = Tools.postRes(@"https://chengyu.911cha.com/", key);
            return str;
        }

        //从HTML中获取成语
        public string[] getFromHtml(string s)
        {
            string[] phrases = new string[] { "" };
            try
            {
                List<string> list = new List<string>();
                int start = s.IndexOf("mconbt");
                int end = s.IndexOf(@"</div>", start);
                s = s.Substring(start + 7, end - start - 8);
                foreach (char c in s)
                {
                    if (Tools.isChinese(c.ToString()))
                    {
                        if (list.Count % 4 == 3)
                        {
                            list.Add(c.ToString() + "-");
                        }
                        else
                        {
                            list.Add(c.ToString());
                        }
                    }
                }
                string[] ps = list.ToArray();
                string str = string.Join("", ps);
                str = str.Remove(str.Length - 1);
                phrases = str.Split('-');
                return phrases;
            }
            catch (Exception e)
            {
                Console.WriteLine("没有找到相关成语...");
                return phrases;
            }
        }
    }
}























