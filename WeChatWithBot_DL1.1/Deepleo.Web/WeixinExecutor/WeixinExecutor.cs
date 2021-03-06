﻿/*--------------------------------------------------------------------------
* WeixinExecutor.cs
 *Auth:deepleo
* Date:2013.12.31
* Email:2586662969@qq.com
*--------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Deepleo.Weixin.SDK;
using System.Text;
using System.Text.RegularExpressions;
using Deepleo.Weixin.SDK.Helpers;
using Deepleo.Weixin.SDK.Entities;
using Deepleo.Web.Services;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.IO;
using System.Runtime.Serialization.Json;

namespace Deepleo.Web
{
    public class WeixinExecutor : IWeixinExecutor
    {
        public WeixinExecutor()
        {
        }

        /// <summary>
        /// 说明：带TODO字眼的代码段，需要开发者自行按照自己的业务逻辑实现
        /// </summary>
        /// <param name="message"></param>
        /// <returns>已经打包成xml的用于回复用户的消息包</returns>
        public async Task<string> Execute(WeixinMessage message)
        {
            var result = "";
            var domain = System.Configuration.ConfigurationManager.AppSettings["Domain"];//请更改成你的域名
            var openId = message.Body.FromUserName.Value;
            var myUserName = message.Body.ToUserName.Value;
            //这里需要调用TokenHelper获取Token的，省略了。
            switch (message.Type)
            {
                case WeixinMessageType.Text://文字消息
                    {
                        var msgId = message.Body.MsgId.Value.ToString();
                        string userMessage = message.Body.Content.Value;

                        if (MvcApplication.GetCache(msgId) != null)
                        {
                            result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, MvcApplication.GetCache(msgId));
                        }
                        else
                        {
                            string BotMessage = await MSBot.PostMessage(userMessage);
                            result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, BotMessage);
                            //result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, "欢迎使用，您输入了：" + userMessage +" 要不你发个图片试试？");
                        }
                    }
                    break;
                case WeixinMessageType.Image://图片消息
                    string imageUrl = message.Body.PicUrl.Value;//图片地址
                    string mediaId = message.Body.MediaId.Value;//mediaId

                    string meg = "";
                    try
                    {
                        //meg = await ComputerVisionHelper.MakeAnalyzeImageRequest(message.Body.PicUrl.Value);
                        meg = await ComputerVisionHelper.MakeOCRRequest(message.Body.PicUrl.Value);
                    }
                    catch (Exception ex)
                    {
                        meg = ex.Message;
                    }
                    result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, meg);
                    //result = ReplayPassiveMessageAPI.ReplayImage(openId, myUserName, mediaId);
                    break;

                case WeixinMessageType.Video://视频消息
                    #region 视频消息
                    {
                        var media_id = message.Body.MediaId.Value.ToString();
                        var thumb_media_id = message.Body.ThumbMediaId.Value.ToString();
                        var msgId = message.Body.MsgId.Value.ToString();
                        //TODO
                        result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("视频消息:openid:{0},media_id:{1},thumb_media_id:{2},msgId:{3}", openId, media_id, thumb_media_id, msgId));
                    }
                    #endregion
                    break;
                case WeixinMessageType.Voice://语音消息
                    #region 语音消息
                    {
                        /*
                        var media_id = message.Body.MediaId.Value.ToString();
                        var format = message.Body.Format.Value.ToString();
                        var msgId = message.Body.MsgId.Value.ToString();
                        var messageRec = message.Body.Recognition.Value.ToString();
                        */

                        var media_id = message.Body.MediaId.Value.ToString();
                        var format = message.Body.Format.Value.ToString();
                        var msgId = message.Body.MsgId.Value.ToString();
                        //var messageRec = message.Body.Recognition.Value.ToString();

                        var token = BasicAPI.GetAccessToken("wxb1275f7967642487", "d4624c36b6795d1d99dcf0547af5443d");
                        var url = string.Format("https://file.api.weixin.qq.com/cgi-bin/media/get?access_token={0}&media_id={1}", token, media_id);

                        string content = new BaiduSpeechRecognition().getStrText("en", url, "amr", "8000");

                        result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, content);

                        if (MvcApplication.GetCache(content) == null)
                        {
                            result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, "語音識別錯誤！");
                        }

                        string BotMessage = await MSBot.PostMessage(content);
                        //result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, BotMessage);
                    }
                    #endregion
                    break;
                case WeixinMessageType.Location://地理位置消息
                    #region 地理位置消息
                    {
                        var location_X = message.Body.Location_X.Value.ToString();
                        var location_Y = message.Body.Location_Y.Value.ToString();
                        var scale = message.Body.Scale.Value.ToString();
                        var Label = message.Body.Label.Value.ToString();
                        //TODO
                        result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("地理位置消息: openid:{0},Location_X:{1},Location_Y:{2},Scale:{3},label:{4}", openId, location_X, location_Y, scale, Label));
                    }
                    #endregion
                    break;
                case WeixinMessageType.Link://链接消息
                    #region 链接消息
                    {
                        var title = message.Body.Title.Value.ToString();
                        var description = message.Body.Description.Value.ToString();
                        var url = message.Body.Url.Value.ToString();
                        var msgId = message.Body.MsgId.Value.ToString();
                        //TODO
                        result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("openid:{0},title:{1},description:{2},url:{3},msgId:{4}", openId, title, description, url, msgId));
                    }
                    #endregion
                    break;
                case WeixinMessageType.Event:
                    string eventType = message.Body.Event.Value.ToLower();
                    string eventKey = string.Empty;
                    try
                    {
                        eventKey = message.Body.EventKey.Value;
                    }
                    catch { }
                    switch (eventType)
                    {
                        case "subscribe"://用户未关注时，进行关注后的事件推送
                            #region 首次关注
                            var token = WeixinConfig.TokenHelper.GetToken();

                            //TODO: 获取用户基本信息后，将用户信息存储在本地。
                            //var weixinInfo = UserAdminAPI.GetInfo(token, openId);//注意：订阅号没有此权限

                            if (!string.IsNullOrEmpty(eventKey))
                            {
                                var qrscene = eventKey.Replace("qrscene_", "");//此为场景二维码的场景值
                                result = ReplayPassiveMessageAPI.RepayNews(openId, myUserName,
                                    new WeixinNews
                                    {
                                        title = "欢迎订阅，场景值：" + qrscene,
                                        description = "欢迎订阅，场景值：" + qrscene,
                                        picurl = string.Format("{0}/ad.jpg", domain),
                                        url = domain
                                    });
                            }
                            else
                            {
                                result = ReplayPassiveMessageAPI.RepayNews(openId, myUserName,
                                 new WeixinNews
                                 {
                                     title = "欢迎订阅",
                                     description = "欢迎订阅，点击此消息查看在线demo",
                                     picurl = string.Format("{0}/ad.jpg", domain),
                                     url = domain
                                 });
                            }
                            #endregion
                            break;
                        case "unsubscribe"://取消关注
                            #region 取消关注
                            result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, "欢迎再来");
                            #endregion
                            break;
                        case "scan":// 用户已关注时的事件推送
                            #region 已关注扫码事件
                            if (!string.IsNullOrEmpty(eventKey))
                            {
                                var qrscene = eventKey.Replace("qrscene_", "");//此为场景二维码的场景值
                                result = ReplayPassiveMessageAPI.RepayNews(openId, myUserName,
                                    new WeixinNews
                                    {
                                        title = "欢迎使用，场景值：" + qrscene,
                                        description = "欢迎使用，场景值：" + qrscene,
                                        picurl = string.Format("{0}/ad.jpg", domain),
                                        url = domain
                                    });
                            }
                            else
                            {
                                result = ReplayPassiveMessageAPI.RepayNews(openId, myUserName,
                                 new WeixinNews
                                 {
                                     title = "欢迎使用",
                                     description = "欢迎订阅，点击此消息查看在线demo",
                                     picurl = string.Format("{0}/ad.jpg", domain),
                                     url = domain
                                 });
                            }
                            #endregion
                            break;
                        case "masssendjobfinish"://事件推送群发结果,
                            #region 事件推送群发结果
                            {
                                var msgId = message.Body.MsgID.Value;
                                var msgStatus = message.Body.Status.Value;//“send success”或“send fail”或“err(num)” 
                                //send success时，也有可能因用户拒收公众号的消息、系统错误等原因造成少量用户接收失败。
                                //err(num)是审核失败的具体原因，可能的情况如下：err(10001)涉嫌广告, err(20001)涉嫌政治, err(20004)涉嫌社会, err(20002)涉嫌色情, err(20006)涉嫌违法犯罪,
                                //err(20008)涉嫌欺诈, err(20013)涉嫌版权, err(22000)涉嫌互推(互相宣传), err(21000)涉嫌其他
                                var totalCount = message.Body.TotalCount.Value;//group_id下粉丝数；或者openid_list中的粉丝数
                                var filterCount = message.Body.FilterCount.Value;//过滤（过滤是指特定地区、性别的过滤、用户设置拒收的过滤，用户接收已超4条的过滤）后，准备发送的粉丝数，原则上，FilterCount = SentCount + ErrorCount
                                var sentCount = message.Body.SentCount.Value;//发送成功的粉丝数
                                var errorCount = message.Body.FilterCount.Value;//发送失败的粉丝数
                                //TODO:开发者自己的处理逻辑,这里用log4net记录日志
                                LogWriter.Default.WriteInfo(string.Format("mass send job finishe,msgId:{0},msgStatus:{1},totalCount:{2},filterCount:{3},sentCount:{4},errorCount:{5}", msgId, msgStatus, totalCount, filterCount, sentCount, errorCount));
                            }
                            #endregion
                            break;
                        case "templatesendjobfinish"://模版消息结果,
                            #region 模版消息结果
                            {
                                var msgId = message.Body.MsgID.Value;
                                var msgStatus = message.Body.Status.Value;//发送状态为成功: success; 用户拒绝接收:failed:user block; 发送状态为发送失败（非用户拒绝）:failed: system failed
                                //TODO:开发者自己的处理逻辑,这里用log4net记录日志
                                LogWriter.Default.WriteInfo(string.Format("template send job finish,msgId:{0},msgStatus:{1}", msgId, msgStatus));
                            }
                            #endregion
                            break;
                        case "location"://上报地理位置事件
                            #region 上报地理位置事件
                            var lat = message.Body.Latitude.Value.ToString();
                            var lng = message.Body.Longitude.Value.ToString();
                            var pcn = message.Body.Precision.Value.ToString();
                            //TODO:在此处将经纬度记录在数据库,这里用log4net记录日志
                            LogWriter.Default.WriteInfo(string.Format("openid:{0} ,location,lat:{1},lng:{2},pcn:{3}", openId, lat, lng, pcn));
                            #endregion
                            break;
                        case "voice"://语音消息
                            #region 语音消息
                            //A：已开通语音识别权限的公众号
                            var userVoice = message.Body.Recognition.Value;//用户语音消息文字
                            result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, "您说:" + userVoice);

                            //B：未开通语音识别权限的公众号
                            var userVoiceMediaId = message.Body.MediaId.Value;//media_id
                            //TODO:调用自定义的语音识别程序识别用户语义

                            #endregion
                            break;
                        case "image"://图片消息
                            #region 图片消息
                            var userImage = message.Body.PicUrl.Value;//用户语音消息文字
                            result = ReplayPassiveMessageAPI.RepayNews(openId, myUserName, new WeixinNews
                            {
                                title = "您刚才发送了图片消息",
                                picurl = string.Format("{0}/Images/ad.jpg", domain),
                                description = "点击查看图片",
                                url = userImage
                            });
                            #endregion
                            break;
                        case "click"://自定义菜单事件
                            #region 自定义菜单事件
                            {
                                switch (eventKey)
                                {
                                    case "myaccount"://CLICK类型事件举例
                                        #region 我的账户
                                        result = ReplayPassiveMessageAPI.RepayNews(openId, myUserName, new List<WeixinNews>()
                                    {
                                        new WeixinNews{
                                            title="我的帐户",
                                            url=string.Format("{0}/user?openId={1}",domain,openId),
                                            description="点击查看帐户详情",
                                            picurl=string.Format("{0}/Images/ad.jpg",domain)
                                        },
                                    });
                                        #endregion
                                        break;
                                    case "www.weixinsdk.net"://VIEW类型事件举例，注意：点击菜单弹出子菜单，不会产生上报。
                                        //TODO:后台处理逻辑
                                        break;
                                    default:
                                        result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, "没有响应菜单事件");
                                        break;
                                }
                            }
                            #endregion
                            break;
                        case "view"://点击菜单跳转链接时的事件推送
                            #region 点击菜单跳转链接时的事件推送
                            result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("您将跳转至：{0}", eventKey));
                            #endregion
                            break;
                        case "scancode_push"://扫码推事件的事件推送
                            {
                                var scanType = message.Body.ScanCodeInfo.ScanType.Value;//扫描类型，一般是qrcode
                                var scanResult = message.Body.ScanCodeInfo.ScanResult.Value;//扫描结果，即二维码对应的字符串信息
                                result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("您扫描了二维码,scanType：{0},scanResult:{1},EventKey:{2}", scanType, scanResult, eventKey));
                            }
                            break;
                        case "scancode_waitmsg"://扫码推事件且弹出“消息接收中”提示框的事件推送
                            {
                                var scanType = message.Body.ScanCodeInfo.ScanType.Value;//扫描类型，一般是qrcode
                                var scanResult = message.Body.ScanCodeInfo.ScanResult.Value;//扫描结果，即二维码对应的字符串信息
                                result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("您扫描了二维码,scanType：{0},scanResult:{1},EventKey:{2}", scanType, scanResult, eventKey));
                            }
                            break;
                        case "pic_sysphoto"://弹出系统拍照发图的事件推送
                            {
                                var count = message.Body.SendPicsInfo.Count;//发送的图片数量
                                var picList = message.Body.PicList;//发送的图片信息
                                result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("弹出系统拍照发图,count：{0},EventKey:{1}", count, eventKey));
                            }
                            break;
                        case "pic_photo_or_album"://弹出拍照或者相册发图的事件推送
                            {
                                var count = message.Body.SendPicsInfo.Count.Value;//发送的图片数量
                                var picList = message.Body.PicList.Value;//发送的图片信息
                                result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("弹出拍照或者相册发图,count：{0},EventKey:{1}", count, eventKey));
                            }
                            break;
                        case "pic_weixin"://弹出微信相册发图器的事件推送
                            {
                                var count = message.Body.SendPicsInfo.Count.Value;//发送的图片数量
                                var picList = message.Body.PicList.Value;//发送的图片信息
                                result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("弹出微信相册发图器,count：{0},EventKey:{1}", count, eventKey));
                            }
                            break;
                        case "location_select"://弹出地理位置选择器的事件推送
                            {
                                var location_X = message.Body.SendLocationInfo.Location_X.Value;//X坐标信息
                                var location_Y = message.Body.SendLocationInfo.Location_Y.Value;//Y坐标信息
                                var scale = message.Body.SendLocationInfo.Scale.Value;//精度，可理解为精度或者比例尺、越精细的话 scale越高
                                var label = message.Body.SendLocationInfo.Label.Value;//地理位置的字符串信息
                                var poiname = message.Body.SendLocationInfo.Poiname.Value;//朋友圈POI的名字，可能为空  
                                result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("弹出地理位置选择器,location_X：{0},location_Y:{1},scale:{2},label:{3},poiname:{4},eventKey:{5}", location_X, location_Y, scale, label, poiname, eventKey));
                            }
                            break;
                        case "card_pass_check"://生成的卡券通过审核时，微信会把这个事件推送到开发者填写的URL。
                            {
                                var cardid = message.Body.CardId.Value;//CardId
                                result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("您的卡券已经通过审核"));
                            }
                            break;
                        case "card_not_pass_check"://生成的卡券未通过审核时，微信会把这个事件推送到开发者填写的URL。
                            {
                                var cardid = message.Body.CardId.Value;//CardId

                            }
                            break;
                        case "user_get_card"://用户在领取卡券时，微信会把这个事件推送到开发者填写的URL。
                            {
                                var cardid = message.Body.CardId.Value;//CardId
                                var isGiveByFriend = message.Body.IsGiveByFriend.Value;//是否为转赠，1代表是，0代表否。
                                var fromUserName = message.Body.FromUserName.Value;//领券方帐号（一个OpenID）
                                var friendUserName = message.Body.FriendUserName.Value;//赠送方账号（一个OpenID），"IsGiveByFriend”为1时填写该参数。
                                var userCardCode = message.Body.UserCardCode.Value;//code序列号。自定义code及非自定义code的卡券被领取后都支持事件推送。
                                var outerId = message.Body.OuterId.Value;//领取场景值，用于领取渠道数据统计。可在生成二维码接口及添加JSAPI接口中自定义该字段的整型值。

                            }
                            break;
                        case "user_del_card"://用户在删除卡券时，微信会把这个事件推送到开发者填写的URL
                            {
                                var cardid = message.Body.CardId.Value;//CardId
                                var userCardCode = message.Body.UserCardCode.Value;//商户自定义code值。非自定code推送为空
                            }
                            break;
                        case "merchant_order"://微信小店：订单付款通知:在用户在微信中付款成功后，微信服务器会将订单付款通知推送到开发者在公众平台网站中设置的回调URL（在开发模式中设置）中，如未设置回调URL，则获取不到该事件推送。
                            {
                                var orderId = message.Body.OrderId.Value;//CardId
                                var orderStatus = message.Body.OrderStatus.Value;//OrderStatus
                                var productId = message.Body.ProductId.Value;//ProductId
                                var skuInfo = message.Body.SkuInfo.Value;//SkuInfo

                            }
                            break;
                    }
                    break;
                default:
                    result = ReplayPassiveMessageAPI.RepayText(openId, myUserName, string.Format("未处理消息类型:{0}", message.Type));
                    break;
            }
            return result;
        }
    }

    //#region
    //public class OCRHelper
    //{
    //    public static async Task<string> MakeRequest(string URL)
    //    {
    //        var client = new System.Net.Http.HttpClient();
    //        var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);

    //        // Request headers
    //        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "9459d73a95e34e5c8e15ec1bb2802d20");

    //        // Request parameters
    //        queryString["language"] = "unk";
    //        queryString["detectOrientation "] = "true";
    //        var uri = "https://api.projectoxford.ai/vision/v1.0/ocr?" + queryString;

    //        HttpResponseMessage response;

    //        // Request body
    //        //byte[] byteData = Encoding.UTF8.GetBytes("{\"url\":\"https://portalstoragewuprod.azureedge.net/vision/OpticalCharacterRecognition/5.jpg\"}");
    //        byte[] byteData = Encoding.UTF8.GetBytes("{\"url\":\"" + URL + "\"}");

    //        using (var content = new ByteArrayContent(byteData))
    //        {
    //            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    //            //application/octet-stream

    //            response = await client.PostAsync(uri, content);

    //            string JSON = await response.Content.ReadAsStringAsync();

    //            RootObject ro = JsonHelper.Deserialize<RootObject>(JSON);

    //            StringBuilder sb = new StringBuilder();

    //            foreach (var region in ro.regions)
    //            {
    //                foreach (var line in region.lines)
    //                {
    //                    foreach (var word in line.words)
    //                    {
    //                        sb.Append(word.text + " ");
    //                    }
    //                    sb.AppendLine(" ");
    //                }
    //                sb.AppendLine(" ");
    //            }

    //            return sb.ToString();
    //        }

    //    }
    //}

    //[DataContract]
    //public class Word
    //{
    //    [DataMember]
    //    public string boundingBox { get; set; }
    //    [DataMember]
    //    public string text { get; set; }
    //}
    //[DataContract]
    //public class Line
    //{
    //    [DataMember]
    //    public string boundingBox { get; set; }
    //    [DataMember]
    //    public List<Word> words { get; set; }
    //}
    //[DataContract]
    //public class Region
    //{
    //    [DataMember]
    //    public string boundingBox { get; set; }
    //    [DataMember]
    //    public List<Line> lines { get; set; }
    //}
    //[DataContract]
    //public class RootObject
    //{
    //    [DataMember]
    //    public string language { get; set; }
    //    [DataMember]
    //    public double textAngle { get; set; }
    //    [DataMember]
    //    public string orientation { get; set; }
    //    [DataMember]
    //    public List<Region> regions { get; set; }
    //}


    //public class JsonHelper
    //{
    //    /// <summary>
    //    /// 将JSON字符串反序列化成数据对象
    //    /// </summary>
    //    /// <typeparam name="T">数据对象类型</typeparam>
    //    /// <param name="json">JSON字符串</param>
    //    /// <returns>返回数据对象</returns>
    //    public static T Deserialize<T>(string json)
    //    {
    //        var _Bytes = Encoding.Unicode.GetBytes(json);
    //        using (MemoryStream _Stream = new MemoryStream(_Bytes))
    //        {
    //            var _Serializer = new DataContractJsonSerializer(typeof(T));
    //            return (T)_Serializer.ReadObject(_Stream);
    //        }
    //    }
    //}
    //#endregion
}