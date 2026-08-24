using System.Collections.Generic;

public class OrderListResponse
{
    public int code { get; set; }
    public string msg { get; set; }
    public OrderListData data { get; set; }
}

public class OrderListData
{
    public int total { get; set; }
    public int per_page { get; set; }
    public int current_page { get; set; }
    public int last_page { get; set; }
    public List<OrderItem> data { get; set; }
}

public class OrderItem
{public int freeze_num { get; set; }
   
    public int store_flag { get; set; }
    public long listing_no { get; set; }
    public long oid { get; set; }
    public string breed_no { get; set; }
    public int type { get; set; }
    public string price { get; set; }
    public int num { get; set; }
    public string bond { get; set; }
    public int deal_num { get; set; }
    public int remain_num { get; set; }
    public int oflag { get; set; }
    public string status { get; set; }
    //ocata_name
    public string ocata_name { get; set; }
    public long time { get; set; }
    public long weituo_time { get; set; }
    public string weituo_price { get; set; }
    public int b_id { get; set; }

}
