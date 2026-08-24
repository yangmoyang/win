using System;
using System.Drawing;
using System.Windows.Forms;

namespace winok
{
    public partial class Form1
    {
        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (_timer1Busy)
                return;

            _timer1Busy = true;
            try
            {
            // return;
            timer1.Interval = 22000;
            if (DateTime.Now > _login.ExpireAt)
            {
                Application.Exit();
            }
            //if (tiao_xu >=80 )
            //{
            //    client.SendRaw(new byte[] { 0xC0, 0x00 });
            //    // client2.SendRaw(new byte[] { 0xC0, 0x00 });
            //    Console.WriteLine("faxintiao");
            //    timer1.Interval = 5000;
            //    return;
            //}
            string yy2 = "";
            //if (new_jr == 3)
            //{
            //    if (honglan_jiaoyi)
            //    {
            //        yy2 = "a";
            //    }
            //    else
            //    {
            //        yy2 = "b";
            //    }
            //}
            if (_huaxian == -3)
            {
                //if(label32.ForeColor==Color.Red)
                //    label32.ForeColor = Color.White;
                //else
                label32.ForeColor = Color.Red;

                label32.Text = "多" + yy2;
            }
            if (_huaxian == 1)
            {
                //if (label32.ForeColor == Color.Blue)
                //    label32.ForeColor = Color.White;
                //else
                label32.ForeColor = Color.Blue;

                label32.Text = "空" + yy2;
            }
            if (_Closeprice < 100)
            {
                timer1.Interval = 1000;
                return;
            }
            shua_jishi++;
            label41.Text = _Closeprice.ToString();

            //if (sheng_shi>15 && sheng_shi < 20 && shua_jishi > 8)
            //{
            //    Console.WriteLine("shaushuashau");
            //    await LoadOrdersAsync2();
            //    shua_jishi = 0;
            //}
            //if (_shua5)
            //{

            //    _shua5 = false;
            //    await LoadOrdersAsync();
            //    await LoadOrdersAsync2();
            //}
            //if (_shua6)
            //{

            //    _shua6 = false;

            //    await LoadOrdersAsync();
            //    await LoadOrdersAsync2();
            //}
            string yy1 = "";

            if (t_yinyang > 0)
            {
                yy1 = "阳连" + t_yinyang.ToString();
            }
            if (t_yinyang < 0)
            {
                yy1 = "阴连" + (-t_yinyang).ToString();
            }

            //label39.Text = zoushi_z.Count.ToString() + " " + yy1 + "   [订] " + t_list2.Count.ToString() + " [未成交]" + t_list.Count.ToString();
            //string zss = "";
            //for (int i = zoushi_z.Count - 1; i >= 0; i--)
            //{
            //    string t2 = "";
            //    if (zoushi_z[i].t_wz)
            //    {
            //        t2 = "[" + zoushi_z[i].s_zs + "]";
            //    }
            //    else
            //    {
            //        t2 = zoushi_z[i].s_zs;
            //    }

            //    zss = t2 + " " + zss;

            //    if (i < zoushi_z.Count - 5)
            //    {
            //        break;
            //    }
            //}

            //label45.Text = zss;

            ////
            /////
            if (_qingkong)
            {
                if (_Closeprice < 100)
                {
                    timer1.Interval = 1500;
                    return;
                }
                if (t_list2.Count == 0 && t_list.Count == 0)
                {
                    timer1.Interval = 1500;
                    AppendLog("清空完毕！");
                    _qingkong = false;
                    // button14.Enabled = true;
                    return;
                }
                //先检测卖
                //  Console.WriteLine("t:" + t_list.Count.ToString() + "," + t_list2.Count.ToString());
                // if (false)
                if (sheng_shi > 20)
                {

                    if (sheng_shi < 15)
                    {
                        foreach (var o in t_list)
                        {
                            bool f1 = true;
                            Console.WriteLine("qingkong:" + o.listing_no.ToString());
                            foreach (StrategyContext n_sc in _strategyList)
                            {
                                if (n_sc.Oid == o.listing_no)
                                {
                                    f1 = false;
                                    break;
                                }

                            }
                            if (!_chedanzu.Contains(o.listing_no) && f1)
                            {
                                AppendLog("[有未完成订货单2]" + o.listing_no.ToString() + " " + o.type.ToString());
                                byte[] packet = BuildcancelOrderPacket(
          user_id,
          o.listing_no,
          _clientId
     );
                                _chedanzu.Add(o.listing_no);
                                // tiao_xu = 0;

                                client.SendRaw(packet);
                                // await LoadOrdersAsync();
                                timer1.Interval = 3000;
                                return;
                            }
                        }
                    }
                    if (sheng_shi > 21)
                    {
                        foreach (var o in t_list2)
                        {
                            bool find = true;
                            if (_chedanzu.Contains(o.oid))
                            {

                                continue;
                            }
                            //foreach (StrategyContext n_sc in _strategyList)
                            //{
                            //    if (n_sc.listing_no == o.oid)
                            //    {
                            //        find = false;

                            //        break;
                            //    }

                            //}
                            if (find)
                            {
                                string kongduo_s = "";
                                int yingkui1 = _Closeprice - (int)float.Parse(o.price);
                                if (o.type == 2)
                                {
                                    yingkui1 = (int)float.Parse(o.price) - _Closeprice;
                                    kongduo_s = "做空";
                                }
                                else
                                {
                                    kongduo_s = "做多";
                                }


                                if (o.freeze_num == 1)
                                {
                                    AppendLog("有计划外单 [" + o.price.ToString() + "]" + kongduo_s + " 已挂 转货单" + yingkui1);
                                }
                                else
                                {

                                    if (kongduo_s == "做多")
                                    {
                                        //  if (yingkui1 >= _Scan.zhiying || yingkui1 <= _Scan.zhisun)
                                        {
                                            byte[] packet = BuildzhuanOrderPacket(user_id, 1, o.num, 2, 2, _Closeprice, 1, o.oid, 1, _clientId);
                                            _chedanzu.Add(o.oid);
                                            client.SendRaw(packet);
                                            timer1.Interval = 4500;
                                            // _shua4 = true;

                                            // tiao_xu = 0;
                                            AppendLog("清空 [" + o.price.ToString() + "]" + kongduo_s + " 未挂 转货单" + yingkui1);
                                            return;
                                        }



                                    }
                                    if (kongduo_s == "做空")
                                    {
                                        //  if (yingkui1 >= _Scan.zhiying || yingkui1 <= _Scan.zhisun)
                                        {
                                            byte[] packet = BuildzhuanOrderPacket(user_id, 1, o.num, 2, 1, _Closeprice, 1, o.oid, 1, _clientId);

                                            _chedanzu.Add(o.oid);
                                            client.SendRaw(packet);
                                            // tiao_xu = 0;
                                            // _shua4 = true;
                                            timer1.Interval = 4500;
                                            AppendLog("清空 [" + o.price.ToString() + "]" + kongduo_s + " 未挂 转货单" + yingkui1);
                                            return;
                                        }


                                    }









                                }


                            }


                        }


                    }

                    timer1.Interval = 15000;

                    return;
                }
            }


          


            if (liveStrategyRunning)
            {




                //
                int wei1 = 0;
                int wei2 = 0;
                int yingkui = 0;
                foreach (StrategyContext n_sc in _strategyList)
                {
                    if (n_sc.buzhou == 8)
                    {
                        wei2 += 1;
                        if (n_sc.duokong == 1)
                        {
                            yingkui += (n_sc.shoujia - n_sc.maijia) * n_sc.beishu;
                        }
                        if (n_sc.duokong == 2)
                        {
                            yingkui += (n_sc.maijia - n_sc.shoujia) * n_sc.beishu;
                        }
                    }

                    if (n_sc.buzhou < 8)
                    {
                        wei1 += 1;

                    }

                }
                // yingkui = _startkeyong - _nowjq;
                label4.Text = (wei1 + wei2).ToString();
                label5.Text = (wei1).ToString();
                label7.Text = (yingkui).ToString();
                if (yingkui >= _Scan.z_zhiying)
                {
                    _qingkong = true;
                    AppendLog("达到总止盈！");
                    button2_Click(null, null);
                   
                    timer1.Interval = 2000;
                    return;
                }
                if (yingkui <= _Scan.z_zhisun)
                {
                    _qingkong = true;
                    AppendLog("达到总止损！");
                    button2_Click(null, null);
                    timer1.Interval = 2000;
                    return;
                }
                if (DateTime.Now >= _Scan.dingshi)
                {
                    _qingkong = true;


                    AppendLog("到指定时间，停止挂机");
                    timer1.Interval = 2000;
                    button2_Click(null, null);
                    return;

                }
                //if (sheng_shi < 30)
                //{
                //    if (_shua5)
                //    {

                //        _shua5 = false;
                //        await LoadOrdersAsync();
                //        await LoadOrdersAsync2();
                //    }
                //}
                foreach (StrategyContext n_sc in _strategyList)
                {
                    if (n_sc.buzhou == 1 || n_sc.buzhou > 5)
                    {
                        continue;
                    }
                    bool fk = true;
                    foreach (var o in t_list2)
                    {
                        if (o.listing_no == n_sc.Oid || o.listing_no == n_sc.oid2)
                        {
                            fk = false;
                            break;
                        }
                    }
                    if (fk)
                    {
                        foreach (var o in t_list)
                        {
                            if (o.listing_no == n_sc.Oid || o.listing_no == n_sc.oid2)
                            {
                                fk = false;
                                break;
                            }
                        }

                    }
                    if (fk)
                    {
                        if (n_sc.buzhou > 1 && n_sc.buzhou < 6 && n_sc.shizhan)
                            n_sc.buzhou = 11;
                        Console.WriteLine("??...");
                    }


                }


                if (sheng_shi > 30 && _Closeprice > 100)
                {

                    //if (_shua4)
                    //{
                    //    await LoadOrdersAsync2();
                    //    _shua4 = false;
                    //}
                    //   if (sheng_shi > 30) { _shua5 = true; }


                    foreach (var o in t_list2)
                    {
                        bool find = true;
                        if (_chedanzu.Contains(o.oid))
                        {

                            continue;
                        }
                        foreach (StrategyContext n_sc in _strategyList)
                        {
                            if (n_sc.listing_no == o.oid)
                            {
                                find = false;

                                break;
                            }

                        }
                        if (find)
                        {
                            string kongduo_s = "";
                            int yingkui1 = _Closeprice - (int)float.Parse(o.price);
                            if (o.type == 2)
                            {
                                yingkui1 = (int)float.Parse(o.price) - _Closeprice;
                                kongduo_s = "做空";
                            }
                            else
                            {
                                kongduo_s = "做多";
                            }


                            if (o.freeze_num == 1)
                            {
                                AppendLog("有计划外单 [" + o.price.ToString() + "]" + kongduo_s + " 已挂 转货单" + yingkui1);
                            }
                            else
                            {
                                if (liveStrategyRunning)
                                {
                                    if (kongduo_s == "做多")
                                    {
                                        if (yingkui1 >= _Scan.zhiying || yingkui1 <= _Scan.zhisun)
                                        {
                                            byte[] packet = BuildzhuanOrderPacket(user_id, 1, o.num, 2, 2, _Closeprice, 1, o.oid, 1, _clientId);
                                            _chedanzu.Add(o.oid);
                                            client.SendRaw(packet);
                                            timer1.Interval = 4500;
                                            // _shua4 = true;

                                            // tiao_xu = 0;
                                            AppendLog("有计划外单 [" + o.price.ToString() + "]" + kongduo_s + " 未挂 转货单" + yingkui1);
                                            return;
                                        }



                                    }
                                    if (kongduo_s == "做空")
                                    {
                                        if (yingkui1 >= _Scan.zhiying || yingkui1 <= _Scan.zhisun)
                                        {
                                            byte[] packet = BuildzhuanOrderPacket(user_id, 1, o.num, 2, 1, _Closeprice, 1, o.oid, 1, _clientId);

                                            _chedanzu.Add(o.oid);
                                            client.SendRaw(packet);
                                            // tiao_xu = 0;
                                            // _shua4 = true;
                                            timer1.Interval = 4500;
                                            AppendLog("有计划外单 [" + o.price.ToString() + "]" + kongduo_s + " 未挂 转货单" + yingkui1);
                                            return;
                                        }


                                    }

                                }







                            }


                        }


                    }

                }

                //bool f4 = false;
                //foreach (StrategyContext n_sc in _strategyList)
                //{
                //    if (n_sc.buzhou == 2 && n_sc.Oid < 1)
                //    {
                //        f4 = true;
                //    }
                //    if (n_sc.buzhou == 4 && n_sc.oid2 < 1)
                //    {
                //        f4 = true;
                //    }
                //}
                //if (f4)
                //{
                //    //Console.WriteLine("???");
                //    timer1.Interval = 2000;
                //    return;
                //}

                bool f2 = false;


                //先检测卖
                if (sheng_shi > 55 && _xiantype != 0)
                {
                    int chuan = _Closeprice;

                    if (false)
                    {
                        int yk = 0;
                        foreach (StrategyContext n_sc in _strategyList)
                        {
                            if (n_sc.buzhou < 6)
                            {
                                if (n_sc.duokong == 1)
                                {
                                    yk += (_Closeprice - n_sc.maijia) * n_sc.beishu;

                                }
                                if (n_sc.duokong == 2)
                                {
                                    yk += (n_sc.maijia - _Closeprice) * n_sc.beishu;
                                }


                            }
                        }
                        if (yk >= n4_zhiying)
                        {
                            n4_chushou = true;
                            _quanmai = true;
                            n4_nowprice = -1;
                            AppendLog("本轮结束，止盈全卖");
                        }
                        if (yk <= n4_zhisun)
                        {
                            n4_chushou = true;
                            _quanmai = true;
                            n4_nowprice = -1;
                            AppendLog("本轮结束，止损全卖");
                        }
                        n4_nowyingkui = yk;

                    }



                    foreach (StrategyContext n_sc in _strategyList)
                    {
                        int oflag = 2;
                        if (n_sc.duokong == 1 && n_sc.buzhou == 3)
                        {

                            //if((_Closeprice-n_sc.maijia)>=

                            if (( (chuan - n_sc.maijia) >= _Scan.zhiying) || _quanmai || ((new_jr < 2) && qie))
                            {
                                byte[] packet = BuildzhuanOrderPacket(user_id, 1, n_sc.beishu, oflag, 2, chuan, 1, n_sc.listing_no, 1, _clientId);

                                //_Xdzt.StrategyId = 1;
                                //_Xdzt.oid = -1;
                                n_sc.OrderSendTime = DateTime.Now;
                                //_Xdzt.listing_no = -1;
                                //_Xdzt.gm_price = int.Parse(textBox4.Text);
                                // Console.WriteLine(user_id, n_sc.beishu, _zx_price, _clientId, n_sc.duokong, n_sc.maimai);
                             
                                //tiao_xu = 0;
                                //  string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
                                n_sc.shoujia = chuan;
                                if (n_sc.shizhan)
                                {
                                    client.SendRaw(packet);
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 止盈销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
                                    n_sc.buzhou = 4;
                                }
                                else
                                {
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]模拟" + n_sc.maijia.ToString() + " 止盈销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
                                    n_sc.buzhou = 8;
                                  
                                }
                                // xd_bianhao = n_sc.StrategyId;
                               
                                n_sc.shitou = false;
                                n_bei_xu = 0;
                                if (new_jr == 3)
                                    _newjr_cuoci = 0;
                                if (quanmai_f)
                                {
                                    _quanmai = true;
                                }
                                if (new_jr == 0)
                                {
                                    if (!_yizhimai)
                                    {
                                        new_jrcan = false;
                                    }
                                }
                                    if (new_jr == 7)
                                    {
                                        new8_cuoci=0;
                                       
                                    }
                                    //if (new_jr < 2)
                                    //{
                                    //    _quanmai = true;
                                    //    //  new_jrcan = false;
                                    //}

                                    timer1.Interval = 1000;
                                return;
                            }
                            // if ((DateTime.Now - n_sc.xiacheng_time).TotalSeconds > _Scan.shoutime)
                            if (((chuan - n_sc.maijia) <= _Scan.zhisun) || _quanmai || ((new_jr < 2) && qie))
                            {
                               
                                n_bei_xu++;
                                if (_values2[n_bei_xu] == 0 || n_bei_xu == 8)
                                {
                                    n_bei_xu = 0;
                                }


                                byte[] packet = BuildzhuanOrderPacket(user_id, 1, n_sc.beishu, oflag, 2, chuan, 1, n_sc.listing_no, 1, _clientId);
                                xd_bianhao = n_sc.StrategyId;
                                n_sc.shoujia = chuan;
                                n_sc.OrderSendTime = DateTime.Now;
                               
                                // string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
                                xd_bianhao = n_sc.StrategyId;
                                if (n_sc.shizhan)
                                {
                                    client.SendRaw(packet);
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 止损销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
                                    n_sc.buzhou = 4;

                                }
                                else
                                {
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]模拟" + n_sc.maijia.ToString() + " 止损销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
                                    n_sc.buzhou = 8;
                                }

                                if (_quanmai_zhisun_f)
                                {
                                    _quanmai = true;
                                }
                                if (new_jr == 0)
                                {
                                    if (!_yizhimai)
                                    {
                                        new_jrcan = false;
                                    }
                                }
                                if(new_jr == 7)
                                    {
                                        new8_cuoci++;
                                        new8_fx = 1;
                                    }
                                _newjr_cuoci++;
                                //if (shouxu == 1)
                                //{
                                //    shouxu = 0;
                                //    if (jixu)
                                //{
                                //    AppendLog("继续介入,手数反方向进行");
                                //}
                                //else
                                //{


                                //        new_jrcan2 = false;
                                //        AppendLog("等待下次机会");

                                //}
                                //}
                                n_sc.shitou = false;
                                //if (quanmai_f)
                                //{
                                //    _quanmai = true;
                                //}
                                timer1.Interval = 1000;
                                return;
                            }
                        }
                        if (n_sc.duokong == 2 && n_sc.buzhou == 3)
                        {
                            oflag = 2;
                            if ((((n_sc.maijia - chuan) >= _Scan.zhiying)) || (_quanmai) || ((new_jr < 2) && qie))
                            {
                                byte[] packet = BuildzhuanOrderPacket(user_id, 1, n_sc.beishu, oflag, 1, chuan, 1, n_sc.listing_no, 1, _clientId);
                                xd_bianhao = n_sc.StrategyId;
                                n_sc.OrderSendTime = DateTime.Now;
                                //_Xdzt.StrategyId = 1;
                                //_Xdzt.oid = -1;
                                //_Xdzt.listing_no = -1;
                                //_Xdzt.gm_price = int.Parse(textBox4.Text);
                                // Console.WriteLine(user_id, n_sc.beishu, _zx_price, _clientId, n_sc.duokong, n_sc.maimai);
                             
                                //  shouxu = 0;
                                // tiao_xu = 0;
                                //string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
                                n_sc.shoujia = chuan;

                                if (n_sc.shizhan)
                                {
                                    client.SendRaw(packet);
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 止盈销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
                                    n_sc.buzhou = 4;
                                }
                                else
                                {
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]模拟" + n_sc.maijia.ToString() + " 止盈销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
                                    n_sc.buzhou = 8;


                                }
                                xd_bianhao = n_sc.StrategyId;
                             
                                n_sc.shitou = false;
                                if (new_jr == 3)
                                    _newjr_cuoci = 0;
                                if (quanmai_f)
                                {
                                    _quanmai = true;
                                    // AppendLog("触发全卖");
                                }
                                n_bei_xu = 0;
                                if (new_jr == 0)
                                {
                                    if (!_yizhimai)
                                    {
                                        new_jrcan = false;
                                    }
                                }
                                    if (new_jr == 7)
                                    {
                                        new8_cuoci = 0;

                                    }
                                    //if (new_jr < 2)
                                    //{
                                    //    // new_jrcan = false;
                                    //    _quanmai = true;
                                    //}
                                    timer1.Interval = 1000;
                                return;
                            }
                            //  if ((DateTime.Now - n_sc.xiacheng_time).TotalSeconds > _Scan.shoutime)
                            if (( ((n_sc.maijia - chuan) <= _Scan.zhisun)) || _quanmai || ((new_jr < 2) && qie))
                            {

                                byte[] packet = BuildzhuanOrderPacket(user_id, 1, n_sc.beishu, oflag, 1, chuan, 1, n_sc.listing_no, 1, _clientId);
                                xd_bianhao = n_sc.StrategyId;
                                n_sc.shoujia = chuan;
                                n_sc.OrderSendTime = DateTime.Now;
                          
                                //tiao_xu = 0;

                                // string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
                                xd_bianhao = n_sc.StrategyId;
                                if (n_sc.shizhan)
                                {
                                    client.SendRaw(packet);
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 止损销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
                                    n_sc.buzhou = 4;
                                }
                                else
                                {
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]模拟" + n_sc.maijia.ToString() + " 止损销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
                                    n_sc.buzhou = 8;
                                }

                               
                                n_sc.shitou = false;
                                if (_quanmai_zhisun_f)
                                {
                                    _quanmai = true;
                                }
                                n_bei_xu++;
                                if (_values2[n_bei_xu] == 0 || n_bei_xu == 8)
                                {
                                    n_bei_xu = 0;
                                }
                                if (new_jr == 0)
                                {
                                    if (!_yizhimai)
                                    {
                                        new_jrcan = false;
                                    }
                                }
                                  
                                    if (new_jr == 7)
                                    {
                                        new8_cuoci++;

                                    }
                                    _newjr_cuoci++;
                                //if (quanmai_f)
                                //{
                                //    _quanmai = true;
                                //}
                                //if (shouxu == 1)
                                //{
                                //    shouxu = 0;
                                //    if (jixu)
                                //    {
                                //        AppendLog("继续介入,手数反方向进行");
                                //    }
                                //    else
                                //    {

                                //        new_jrcan2 = false;
                                //        AppendLog("等待下次机会");

                                //    }
                                //}
                                timer1.Interval = 1000;
                                return;
                            }
                        }


                    }

                }



                if (_quanmai)
                {
                    int a1 = 0;
                    foreach (StrategyContext n_sc in _strategyList)
                    {
                        if (n_sc.buzhou < 8)
                        {
                            if (_Closeprice == n_sc.maijia)
                            {
                                a1++;
                                break;
                            }

                        }


                    }
                    if (a1 > 0)
                    {
                        timer1.Interval = 1500;
                        return;
                    }
                    else
                    {
                        _quanmai = false;
                    }
                    jg_zu.Clear();
                }

                //检测是否购买
                if (t_jiance)// && (_xiantype != 0 || (new_jr == 2))
                {// && (_xiantype != 0 || (new_jr == 2))
                    bool mai_f = true;
                    if (true) //new_jr != 3)
                    {
                        foreach (StrategyContext n_sc in _strategyList)
                        {
                            if (n_sc.buzhou < 8)
                            {

                                if (t_yinyang > 0)
                                {
                                    if ((_Closeprice == n_sc.maijia) && (n_sc.duokong == 1))
                                    {
                                        mai_f = false;
                                        break;
                                    }
                                }
                                if (t_yinyang < 0)
                                {
                                    if ((_Closeprice == n_sc.maijia) && (n_sc.duokong == 2))
                                    {
                                        mai_f = false;
                                        break;
                                    }
                                }




                            }


                        }
                    }


                    // if(t_jiance)
                    if (!mai_f)
                    {

                        if ((new_jr <=2) || _xincelue )
                        {
                            AppendLog("单价[" + _Closeprice.ToString() + "] 未完全成交，放弃进场");
                            t_jiance = false;
                            mai_f = false;
                        }



                    }

                    if ((mai_f))//&& (zoushi_z.Count >= wanzheng_can)
                    {
                        if (sheng_shi > 30)
                        {
                            if (new_jr == 0)
                            {
                                if (new_jrcan)
                                {

                                }
                                else
                                {
                                    new1_dian = 0;
                                    new1_fx = 1;
                                    new1_fw.Clear();
                                }
                              
                            }
                                Console.WriteLine("处理买");
                            bool mai2 = true;

                            if (new_jr == 4)
                            {
                              
                                    if (t_yinyang == 0)
                                    {
                                        mai2 = false;

                                    }
                                    if (mai2)
                                    {
                                        bool f5 = false;
                                        int a7 = 0;
                                        int a8 = 0;
                                        string s9 = "";
                                        if (t_yinyang > 0)
                                        {
                                            a7 = _Closeprice;
                                            s9 = "做多【" + _Closeprice.ToString() + "】";
                                        }
                                        if (t_yinyang < 0)
                                        {
                                            a7 = -_Closeprice;
                                            s9 = "做空【" + _Closeprice.ToString() + "】";
                                        }
                                        for (int i = 0; i < _m_mai.Count; i++)
                                        {
                                            if (i == _m_fenzhong)
                                            {
                                                break;
                                            }
                                            if (a7 == _m_mai[i])
                                            {
                                                f5 = true;
                                                a8 = i + 1;
                                            }
                                        }
                                        if (f5)
                                        {
                                            mai2 = false;
                                            AppendLog(s9 + " " + a8.ToString() + " 分钟内已买过");
                                            AppendLog("结果sd" + a8.ToString() + " 分钟内已买过");
                                            _m4_can = t_yinyang;
                                        }

                                    }
                                }
                                if (new_jr == 6)
                                {
                                    if (!_m4_down || !_m4_up)
                                    {

                                        Console.WriteLine("----");
                                    }
                                    if (shifou_wanzheng)
                                    {
                                        if (_m4_can == 0)
                                        {

                                        }
                                        else
                                        {
                                            if (t_yinyang > 0)
                                            {
                                                if (_m4_can > 0)
                                                {
                                                    mai2 = false;
                                                }


                                            }
                                            if (t_yinyang < 0)
                                            {
                                                if (_m4_can < 0)
                                                {
                                                    mai2 = false;
                                                }
                                            }
                                            //if (mai2)
                                            //{

                                            //    if (t_yinyang > 0)
                                            //    {
                                            //        if (!_m4_up)
                                            //        {
                                            //            mai2 = false;
                                            //            t_jiance = false;
                                            //            _m4_up = true;
                                            //        }
                                            //    }
                                            //    if (t_yinyang < 0)
                                            //    {
                                            //        if (!_m4_down)
                                            //        {
                                            //            mai2 = false;
                                            //            t_jiance = false;
                                            //            _m4_down = true;
                                            //        }
                                            //    }
                                            //    _m4_can = t_yinyang;
                                            //}

                                        }


                                    }
                                    else
                                    {
                                        mai2 = false;
                                        // AppendLog("不完整");
                                    }
                                    if (mai2)
                                    {
                                        if (qie)
                                        {
                                            AppendLog("红蓝切换放弃购买");
                                            AppendLog("结果sd" + "红蓝切换");
                                            mai2 = false;
                                        }
                                    }
                                    if (mai2)
                                    {
                                        bool f5 = false;
                                        int a7 = 0;
                                        int a8 = 0;
                                        string s9 = "";
                                        if (t_yinyang > 0)
                                        {
                                            a7 = _Closeprice;
                                            s9 = "做多【" + _Closeprice.ToString() + "】";
                                        }
                                        if (t_yinyang < 0)
                                        {
                                            a7 = -_Closeprice;
                                            s9 = "做空【" + _Closeprice.ToString() + "】";
                                        }
                                        for (int i = 0; i < _m_mai.Count; i++)
                                        {
                                            if (i == _m_fenzhong)
                                            {
                                                break;
                                            }
                                            if (a7 == _m_mai[i])
                                            {
                                                f5 = true;
                                                a8 = i + 1;
                                            }
                                        }
                                        if (f5)
                                        {
                                            mai2 = false;
                                            AppendLog(s9 + " " + a8.ToString() + " 分钟内已买过");
                                            AppendLog("结果sd" + a8.ToString() + " 分钟内已买过");
                                            _m4_can = t_yinyang;
                                        }

                                    }

                                    if (mai2)
                                    {
                                        _m4_can = t_yinyang;
                                    }


                                    t_jiance = false;

                                }
                                if (new_jr == 1)
                            {
                                bool f4 = true;
                                foreach (StrategyContext n_sc in _strategyList)
                                {
                                    if (n_sc.buzhou < 8)
                                    {
                                        if (m2_price == n_sc.maijia)
                                        {
                                            f4 = false;
                                            break;
                                        }

                                    }


                                }
                                if (f4)
                                {
                                    bool f3 = false;
                                    if (!qie)
                                    {

                                        if (_huaxian == -3)
                                        {
                                            if (Math.Abs(t_yinyang) >= _Scan.jr_can && t_yinyang < 0)
                                            {
                                                f3 = true;
                                            }

                                        }
                                        if (_huaxian == 1)
                                        {
                                            if (t_yinyang >= _Scan.jr_can && t_yinyang > 0)
                                            {
                                                f3 = true;
                                            }
                                        }


                                    }
                                    mai2 = f3;
                                }
                                else
                                {

                                }

                            }

    
                                if(new_jr == 7)
                                {
                                    if (!new8_f)
                                    {
                                        mai2 = false;
                                    }
                                  
                                    if (mai2)
                                    {
                                        if (new8_cuoci>1)
                                        {
                                            if (_huaxian == -3)
                                            {
                                                if (t_yinyang > 0)
                                                {
                                                    new8_cuoci = 0;
                                                }

                                            }
                                            else
                                            {
                                                if (t_yinyang < 0)
                                                {
                                                    new8_cuoci = 0;
                                                }
                                            }
                                           
                                        }
                                        if (new8_cuoci>1)
                                        {
                                            mai2 = false;
                                        }
                                       

                                    }
                                    if (!mai2)
                                    {
                                        t_jiance = false;
                                    }
                                }

                            if (new_jr == 0)// (jr_fujia == 1)
                            {
                                if (new1_fw.Contains(_Closeprice))
                                {
                                    AppendLog("包含"+_Closeprice.ToString());
                                    mai2 = false;
                                }
                                else
                                {
                                    if (new_jrcan)
                                    {
                                        if (new1_dian != 0)
                                        {
                                            if (new1_fx == 1)
                                            {

                                                if (_Closeprice < new1_dian)
                                                {

                                                }
                                                else
                                                {
                                                    mai2 = false;
                                                }

                                            }
                                            if (new1_fx == 2)
                                            {

                                                if (_Closeprice > new1_dian)
                                                {

                                                }
                                                else
                                                {
                                                    mai2 = false;
                                                }

                                            }
                                        }
                                        else
                                        {

                                        }

                                    }
                                    else
                                    {
                                        t_jiance = false;

                                        //AppendLog("当前(做多)状态,累计完整K[" + t_wanzheng.ToString() + "],阴阳不符[" + t_yinyang.ToString() + "]，放弃入场");
                                        mai2 = false;
                                    }
                                }

                              

                            }

                            if (new_jr == 2)
                            {
                                if (new3_buzhou == 0)
                                {
                                    mai2 = false;
                                }
                                if (_suoding.Contains(_Closeprice))
                                {
                                    mai2 = false;
                                    AppendLog("价格在锁定范围，放弃介入");
                                }

                                if (mai2)
                                {


                                    //if (_newjr_cuoci > 0)
                                    //{
                                    //    new3_f = false;
                                    //    _newjr_cuoci = 0;
                                    //}
                                    bool f7 = false;
                                    if (_huaxian == -3)
                                    {
                                        if ((-t_yinyang == 1))//_Scan.jr_can)&&(lianxu_wanzheng>= _Scan.jr_can))
                                        {
                                            //触发
                                            f7 = true;
                                            //   _newjr_cuoci = 0;
                                        }
                                        else
                                        {

                                        }
                                    }

                                    if (_huaxian == 1)
                                    {
                                        if ((t_yinyang == 1))//_Scan.jr_can)&& (lianxu_wanzheng >= _Scan.jr_can))
                                        {
                                            //触发
                                            f7 = true;
                                        }
                                        else
                                        {

                                        }
                                    }
                                    if (f7)
                                    {


                                        //new3_cishu++;
                                        //AppendLog("措辞：" + new3_cishu.ToString() + "/" + _newjr_cuoci.ToString());
                                        //if (_newjr_cuoci > 0)
                                        //{
                                        //    if (new3_cishu > _setcuoci)
                                        //    {
                                        //        //  mai2 = false;
                                        //        _newjr_cuoci = 0;
                                        //    }
                                        //    else
                                        //    {
                                        //        mai2 = false;
                                        //    }
                                        //}
                                        //else
                                        //{
                                        //    mai2 = f7;
                                        //}
                                    }
                                    else
                                    {

                                        mai2 = false;
                                    }
                                    if (mai2)
                                    {

                                        new3_cishu = 0;
                                        //  new3_cangshu = 0;
                                    }
                                    //  }
                                    //  }

                                }
                                else
                                {

                                }

                                if (!mai2)
                                {
                                    t_jiance = false;
                                }

                                // mai2 = f7;
                                //if (new3_f == false)
                                //{
                                //    mai2 = false;
                                //}
                            }



                            if (new_jr == 3)
                            {
                                bool f7 = false;
                                if (_huaxian == -3)
                                {
                                    if (t_yinyang == _Scan.jr_can)
                                    {
                                        //触发
                                        f7 = true;
                                    }
                                    else
                                    {

                                    }
                                }

                                if (_huaxian == 1)
                                {
                                    if (-t_yinyang == _Scan.jr_can)
                                    {
                                        //触发
                                        f7 = true;
                                    }
                                    else
                                    {

                                    }
                                }
                                mai2 = f7;

                            }
                            //if (new_jr == 2)
                            //{
                            //    if (!new_jrcan3)
                            //    {
                            //        mai2 = false;
                            //    }
                            //}

                            if (new_jr == 5)
                            {
                                if (shifou_wanzheng)
                                {
                                    if ((t_yinyang == 2) || (t_yinyang == 3) || (t_yinyang == -2) || (t_yinyang == -3))
                                    {

                                    }
                                    else
                                    {
                                        mai2 = false;
                                    }


                                }


                            }
                            if (new_jr == 4)
                            {
                                // if (tongxiang_f)
                                {
                                    if (jg_zu.Contains(_Closeprice))
                                    {
                                        AppendLog("[" + _Closeprice.ToString() + "] 本方向已进过场，放弃进场");
                                        mai2 = false;
                                        t_jiance = false;
                                    }
                                }
                            }

                            if (jr_price > 0 && new_jr < 2 && false)
                            {
                                if (_huaxian == -3)
                                {
                                    if ((_Closeprice <= (jr_price + _Scan.zhisun)) && (jr_price > 0))
                                    {
                                        //if (jixu)
                                        //{

                                        //}
                                        //else
                                        //{
                                        new_jrcan2 = false;
                                        new_jrcan = false;
                                        new_jrcan3 = false;

                                        jg_zu.Add(_Closeprice);
                                        AppendLog("本轮结束等待下次机会1");
                                        yinyang_qie = false;
                                        jr3_zt = jr_price;
                                        mai2 = false;
                                        //}

                                        jr_price = -1;
                                        shouxu = 0;
                                    }
                                    if ((_Closeprice >= (jr_price + _Scan.zhiying)) && (jr_price > 0))
                                    {
                                        if (jixu)
                                        {

                                        }
                                        else
                                        {
                                            new_jrcan2 = false;
                                            new_jrcan = false;
                                            new_jrcan3 = false;

                                            jg_zu.Add(_Closeprice);
                                            AppendLog("本轮结束等待下次机会2");
                                            yinyang_qie = false;
                                            jr3_zt = -1;
                                            mai2 = false;
                                        }
                                        jr_price = -1;
                                        shouxu = 0;

                                    }
                                }
                                else
                                {
                                    if ((_Closeprice >= (jr_price - _Scan.zhisun)) && (jr_price > 0))
                                    {


                                        //if (jixu)
                                        //{

                                        //}
                                        //else
                                        //{
                                        new_jrcan2 = false;
                                        new_jrcan = false;
                                        new_jrcan3 = false;

                                        jg_zu.Add(_Closeprice);
                                        AppendLog("本轮结束等待下次机会1");
                                        yinyang_qie = false;
                                        jr3_zt = jr_price;
                                        mai2 = false;
                                        //}

                                        jr_price = -1;
                                        shouxu = 0;
                                    }
                                    if ((_Closeprice <= (jr_price - _Scan.zhiying)) && (jr_price > 0))
                                    {
                                        if (jixu)
                                        {

                                        }
                                        else
                                        {
                                            new_jrcan2 = false;
                                            new_jrcan = false;
                                            new_jrcan3 = false;

                                            jg_zu.Add(_Closeprice);
                                            AppendLog("本轮结束等待下次机会2");
                                            yinyang_qie = false;
                                            jr3_zt = -1;
                                            mai2 = false;
                                        }
                                        jr_price = -1;
                                        shouxu = 0;

                                    }
                                }

                            }

                            if (_quanmai && new_jr < 3)
                            {
                                int a1 = 0;
                                for (int i = 0; i < _strategyList.Count; i++)
                                {
                                    StrategyContext n_sc = new StrategyContext();
                                    if (n_sc.buzhou < 4)
                                    {
                                        a1++;
                                    }

                                }
                                if (a1 == 0)
                                {
                                    _quanmai = false;
                                    if (new_jr < 2)
                                    {
                                        // new_jrcan = false;
                                        shouxu = 0;
                                        // Console.WriteLine("" + a1.ToString());
                                    }

                                }
                                else
                                {
                                    //   Console.WriteLine("未全卖！" + a1.ToString());
                                }

                            }
                            if (mai2 && new_jr != 1 && new_jr != 2 && new_jr != 5 && new_jr != 6)
                                {
                                int a1 = 0;
                                for (int i = 0; i < _strategyList.Count; i++)
                                {
                                    StrategyContext n_sc = _strategyList[i];
                                    if (n_sc.buzhou < 6)
                                    {
                                        a1++;
                                    }

                                }
                                if (cangshu == 1)
                                {

                                    if (t_list2.Count > 0)
                                    {
                                        mai2 = false;
                                    }
                                    //if (a1 > 0)
                                    //{
                                    //    mai2 = false;
                                    //}

                                }
                                if (cangshu == 2)
                                {
                                    //if (a1 > 1)
                                    //{
                                    //    mai2 = false;
                                    //}
                                    if (t_list2.Count > 1)
                                    {
                                        mai2 = false;
                                    }
                                }
                                if (cangshu == 3)
                                {
                                    //if (a1 > 0)
                                    //{
                                    //    mai2 = false;
                                    //}

                                }

                            }


                            //if (t_wanzheng < wanzheng_can)
                            //{
                            //    mai2 = false;
                            //}
                            if (qie)
                            {
                                if (new_jr < 2)
                                {
                                    mai2 = false;
                                }

                            }
                            if (!shifou_wanzheng)
                            {
                                mai2 = false;
                            }

                            if (mai2)
                            {


                                StrategyContext n_sc = new StrategyContext();
                                n_sc.StrategyId = _strategySeq++;

                                //if (new_jr == 3)
                                //{
                                //    n_sc.beishu = n4_beishu;
                                //    n4_nowprice = _Closeprice;
                                //    n4_shoushu++;
                                //}
                                //else
                                //{
                                //    if (only_one)
                                //    {
                                //        n_sc.beishu = (int)_values[g_bei_xu];
                                //    }
                                //    else
                                //    {

                                //        n_sc.beishu = (int)nm_z[shouxu].Value;
                                //    }




                                //}
                                //if(new_jr == 4)
                                //{
                                //    n4_beishu = _values2[0];

                                //}




                                string h_s = "";


                                if (new_jr == 0 || new_jr == 1||new_jr == 7)
                                {
                                    if (_huaxian == -3)
                                    {
                                        h_s = "做多[正买]";
                                        n_sc.duokong = 1;

                                        if (new1_dian==0)
                                        {
                                            new1_dian = _Closeprice;
                                            new1_fx = 1;
                                            new1_fw.Add(_Closeprice);
                                        }
                                    }
                                    else
                                    {
                                        h_s = "做空[正买]";
                                        n_sc.duokong = 2;

                                        if (new1_dian == 0)
                                        {
                                            new1_dian = _Closeprice;
                                            new1_fx = 2;
                                            new1_fw.Add(_Closeprice);
                                        }
                                    }

                                        new8_num++;
                                }
                                n_sc.beishu = _Scan.beishu;
                                string bak = "";
                                if (new_jr == 2)
                                {
                                    _suoding.Clear();
                                    _suoding.Add(_Closeprice);
                                    if (t_yinyang < 0)
                                    {
                                        h_s = "做空";
                                        n_sc.duokong = 2;
                                        _suoding.Add(_Closeprice + 1);
                                    }
                                    if (t_yinyang > 0)
                                    {
                                        h_s = "做多";
                                        n_sc.duokong = 1;
                                        _suoding.Add(_Closeprice - 1);
                                    }


                                    if (h_s == "")
                                    {
                                        t_jiance = false;

                                        timer1.Interval = 1000;
                                        //AppendLog("零价格 不买");
                                        return;
                                    }
                                    new3_cangshu++;
                                    bak = " 第 " + new3_cangshu.ToString() + " 仓";



                                }
                                if (new_jr == 3)
                                {

                                    if (jin_zhengfan == 0)
                                    {
                                        if (t_yinyang > 0)
                                        {
                                            h_s = "做多";
                                            n_sc.duokong = 1;
                                        }
                                    }
                                    if (jin_zhengfan == 1)
                                    {
                                        if (t_yinyang < 0)
                                        {
                                            h_s = "做空";
                                            n_sc.duokong = 2;
                                        }
                                    }

                                    if (jin_zhengfan == 2)
                                    {
                                        if (t_yinyang > 0)
                                        {
                                            h_s = "做空";
                                            n_sc.duokong = 2;
                                        }
                                    }
                                    if (jin_zhengfan == 3)
                                    {
                                        if (t_yinyang < 0)
                                        {
                                            h_s = "做多";
                                            n_sc.duokong = 1;
                                        }
                                    }
                                    if (h_s == "")
                                    {
                                        t_jiance = false;

                                        timer1.Interval = 1000;
                                        //AppendLog("零价格 不买");
                                        return;
                                    }


                                }
                                    if (new_jr == 4 || new_jr == 6)
                                    {
                                        if (t_yinyang > 0)
                                        {
                                            h_s = "做多[正买]";
                                            n_sc.duokong = 1;
                                            _m_mai[0] = _Closeprice;

                                        }
                                        if (t_yinyang < 0)
                                        {
                                            h_s = "做空[正买]";
                                            n_sc.duokong = 2;
                                            _m_mai[0] = -_Closeprice;
                                        }

                                    }

                                    if (new_jr == 5)
                                {
                                    if (t_yinyang < 0)
                                    {
                                        h_s = "做多[正买]";
                                        n_sc.duokong = 1;
                                    }
                                    if (t_yinyang > 0)
                                    {
                                        h_s = "做空[正买]";
                                        n_sc.duokong = 2;
                                    }
                                }


                                jg_zu.Add(_Closeprice);
                                if (n_sc.beishu == 0)
                                {
                                    t_jiance = false;

                                    timer1.Interval = 1000;
                                    AppendLog("零价格 不买");
                                    return;
                                }
                                new_jrcan2 = false;
                                new_jrcan = false;

                                    AppendLog("结果  sd" + h_s);
                                    n_sc.maijia = _Closeprice;
                                if (new_jr == 1)
                                {
                                    if (m2_price == 0)
                                    {
                                        m2_price = _Closeprice;
                                    }
                                }

                                if (_shizhan_f)
                                {
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " " + h_s + "->" + _Closeprice.ToString() + " 开始订货=>" + n_sc.beishu.ToString() + " 手");

                                }
                                else
                                {
                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]模拟" + n_sc.maijia.ToString() + " " + h_s + "->" + _Closeprice.ToString() + " 开始订货=>" + n_sc.beishu.ToString() + " 手");


                                }
                                n_sc.buzhou = 1;


                                n_sc.TriggerTime = DateTime.Now;
                                n_sc.maimai = 2;
                                n_sc.shitou = false;
                                n_sc.OrderSendTime = DateTime.Now;

                                int oflag = 1;


                                int chuan = _Closeprice;
                                byte[] packet = BuildPlaceOrderPayload(user_id, n_sc.beishu, chuan, _clientId, n_sc.duokong, oflag, 1, 1, 1);
                                // byte[] packet = BuildPlaceOrderPacket(user_id, n_sc.beishu, chuan, _clientId, n_sc.duokong, oflag, 1, 1, 1);
                                //    byte[] packet = BuildPlaceOrderPacket(user_id, 1, n_sc.beishu, n_sc.maimai, n_sc.duokong, _zx_price, 1, n_sc.listing_no, 1, _clientId);

                                //_Xdzt.StrategyId = 1;
                                //_Xdzt.oid = -1;
                                //_Xdzt.listing_no = -1;
                                //_Xdzt.gm_price = int.Parse(textBox4.Text);
                                //   xd_bianhao = n_sc.StrategyId;
                                if (!jixu)
                                {
                                    if (new_jr < 3)
                                    {
                                        if (jr_price == -1)
                                        {
                                            jr_price = _Closeprice;
                                            // AppendLog("新的介入点: " + jr_price.ToString());
                                            _dayin = "进";
                                        }
                                        else
                                        {
                                            _dayin = "买";
                                        }
                                    }
                                }


                                //if (new_jr == 3)
                                //{
                                //    AppendLog("起步价格: " + n4_price.ToString() + ",  第 " + n4_shoushu.ToString() + " 手");
                                //}


                                _strategyList.Add(n_sc);
                                // string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
                                if (_shizhan_f)
                                {
                                    client.SendRaw(packet);
                                }
                                else
                                {
                                    n_sc.Oid = 10000;
                                    n_sc.buzhou = 3;
                                    n_sc.shizhan = false;

                                }

                                if (new_jr < 3)
                                {
                                    shouxu++;
                                    if (shouxu == (_Scan.zhiying))
                                    {
                                        shouxu = 0;
                                    }
                                    else
                                    {
                                        //if (shouxu < (_Scan.zhiying ))
                                        //{

                                        //}


                                    }
                                    if (shouxu > 5) { shouxu = 0; }


                                }

                                //client.SendRaw(packet, "PLACE_ORDER");
                                // AppendLog("[" + n_sc.StrategyId.ToString() + "]"+n_sc.maijia.ToString()+" 订货成功");
                                t_jiance = false;
                                tiao_xu = 0;
                                //  await LoadOrdersAsync();
                                timer1.Interval = 1000;
                                return;
                            }
                            //  t_jiance = false;

                        }

                    }
                }

                //检查单子都在不


                //没成单的都撤单
                if (sheng_shi < 15)
                {
                    foreach (var o in t_list)
                    {
                        bool f1 = true;
                        Console.WriteLine("jiance:" + o.listing_no.ToString());
                        foreach (StrategyContext n_sc in _strategyList)
                        {
                            if (n_sc.Oid == o.listing_no) //n_sc.oid2 == o.oid
                            {

                                f1 = false;
                                // Console.WriteLine("jiance2:" + o.listing_no.ToString());
                                break;
                            }
                        }
                        if (f1)
                            if (!_chedanzu.Contains(o.listing_no))
                            {
                                AppendLog("[有未完成订货单]" + o.listing_no.ToString() + " " + o.type.ToString());
                                byte[] packet = BuildcancelOrderPacket(
          user_id,
          o.listing_no,
          _clientId
     );
                                _chedanzu.Add(o.listing_no);
                                // tiao_xu = 0;

                                client.SendRaw(packet);
                                // await LoadOrdersAsync();
                                timer1.Interval = 3000;
                                return;
                            }
                    }



                    foreach (StrategyContext n_sc in _strategyList)
                    {
                        if (n_sc.buzhou == 2)
                        {

                            byte[] packet = BuildcancelOrderPacket(
                user_id,
                n_sc.Oid,
                _clientId
           );

                            n_sc.OrderSendTime = DateTime.Now;
                            //  xd_bianhao = n_sc.StrategyId;
                            n_sc.buzhou = 11;
                            client.SendRaw(packet);
                            //string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
                            AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 订货未成单撤单");
                            n_sc.maimai = 0;

                        }

                        if (n_sc.buzhou == 2)
                        {

                            byte[] packet = BuildcancelOrderPacket(
                user_id,
                n_sc.Oid,
                _clientId
           );
                            // tiao_xu = 0;
                            n_sc.OrderSendTime = DateTime.Now;
                            //  xd_bianhao = n_sc.StrategyId;
                            n_sc.buzhou = 11;
                            client.SendRaw(packet);
                            //string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
                            AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 订货未成单撤单");
                            n_sc.maimai = 0;

                        }

                        if (n_sc.buzhou == 5)
                        {

                            byte[] packet = BuildcancelOrderPacket(
             user_id,
             n_sc.oid2,
             _clientId
        );
                            n_sc.OrderSendTime = DateTime.Now;
                            n_sc.buzhou = 3;
                            xd_bianhao = n_sc.StrategyId;
                            client.SendRaw(packet);
                            // tiao_xu = 0;
                            //string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
                            AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 转货未成单撤单");
                            //n_sc.maimai = 0;

                        }

                    }



                }


            }

            //   else
            {
                //全麦
            }
            timer1.Interval = 1000;
            return;
            }
            catch (Exception ex)
            {
                AppendLog("timer1 error: " + ex.Message);
            }
            finally
            {
                _timer1Busy = false;
            }
        }

    }
}
