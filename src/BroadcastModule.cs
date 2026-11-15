using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class BroadcastCard : CardPanel {
  public BroadcastCard(){SetStyle(ControlStyles.Selectable,true);TabStop=true;AccessibleRole=AccessibleRole.PushButton;Cursor=Cursors.Hand;}
  protected override void OnMouseDown(MouseEventArgs e){Focus();base.OnMouseDown(e);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(6,6,Width-12,Height-12),Ui.Accent,Ui.Card);}
 }
 internal sealed class BroadcastModule : ToolModule {
  public override string Title{get{return "广播模板";}}
  public override string Symbol{get{return "◖";}}
  readonly Label feedback;
  readonly List<BroadcastCard> cards=new List<BroadcastCard>();bool arranging,busy,disposed;DateTime nextSync=DateTime.MinValue;string[] current;bool hasCache;

  public BroadcastModule(ToolContext context):base(context){
   Header("广播模板","");
   feedback=Ui.Label(Page,"点击广播内容即可复制。",0,82,936,28,Ui.Small,Ui.Muted);
   current=BroadcastTemplates.Load(BroadcastTemplates.CachePath);hasCache=!object.ReferenceEquals(current,BroadcastTemplates.Defaults);foreach(string text in current)AddTemplate(text);
   Page.SizeChanged+=delegate{ArrangeCards();};ArrangeCards();
  }
  public override void Tick(){
   if(disposed||busy||!Page.Visible||!C.Window.AutomaticTemplateUpdatesEnabled||DateTime.UtcNow<nextSync)return;
   Synchronize();
  }
  async void Synchronize(){
   busy=true;nextSync=DateTime.UtcNow.AddMinutes(5);feedback.Text="正在更新广播模板，当前内容仍可点击复制。";
   try{
    var result=await Task.Run(()=>BroadcastTemplates.Refresh());
    if(disposed)return;
    if(!current.SequenceEqual(result.Templates)){
     Page.SuspendLayout();try{
      foreach(var card in cards)card.Dispose();cards.Clear();current=result.Templates;
      foreach(string text in current)AddTemplate(text);
     }finally{Page.ResumeLayout(true);}
     ArrangeCards();
    }
    hasCache=result.Cached;
    feedback.Text=result.Cached?"模板已更新并缓存到本地 · 点击广播内容即可复制。":"模板已更新，但本地缓存写入失败 · 点击广播内容即可复制。";
    if(current.Length==0)feedback.Text="当前没有广播模板。";
   }catch(Exception ex){
    if(!disposed){feedback.Text=hasCache?"暂时无法更新，正在使用本地模板 · 点击内容即可复制。":"暂时无法更新，正在使用内置模板 · 点击内容即可复制。";Log.Write(ex);}
   }finally{busy=false;}
  }
  void ArrangeCards(){
   if(arranging)return;arranging=true;
   try{
    float scale=C.Window.UiScale;int width=Page.ClientSize.Width;
    feedback.SetBounds(0,(int)(82*scale),width,(int)(28*scale));
    int gap=(int)(24*scale),cardWidth=Math.Max(1,(width-gap)/2),y=(int)(124*scale);
    for(int i=0;i<cards.Count;i+=2){
     int rowHeight=(int)(120*scale);
     for(int k=i;k<Math.Min(i+2,cards.Count);k++){
      var body=cards[k].Controls[0];
      int textHeight=TextRenderer.MeasureText(body.Text,body.Font,new Size(Math.Max(1,cardWidth-(int)(40*scale)),int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
      rowHeight=Math.Max(rowHeight,textHeight+(int)(48*scale));
     }
     for(int k=i;k<Math.Min(i+2,cards.Count);k++)cards[k].SetBounds(k%2*(cardWidth+gap),y,cardWidth,rowHeight);
     y+=rowHeight+(int)(16*scale);
    }
    View.PerformLayout();
   }finally{arranging=false;}
  }
  void AddTemplate(string text){
   var card=new BroadcastCard{Size=new Size(456,120),AccessibleName=text,AccessibleDescription="点击复制广播内容"};
   var body=Ui.Label(card,text,20,20,416,80,Ui.Body,Ui.Text);body.TextAlign=ContentAlignment.TopLeft;body.AutoEllipsis=false;body.UseMnemonic=false;body.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;body.Cursor=Cursors.Hand;
   card.Click+=delegate{Copy(text);};body.Click+=delegate{card.Focus();Copy(text);};
   if(C.Window.UiScale!=1)card.Scale(new SizeF(C.Window.UiScale,C.Window.UiScale));
   cards.Add(card);Page.Controls.Add(card);
  }
  void Copy(string text){
   try{Clipboard.SetDataObject(text,true,5,100);ShowToast("✓ 复制成功",Ui.Accent);}
   catch(ExternalException){ShowToast("复制失败，请重试",Ui.Muted);}
  }
  void ShowToast(string message,Color color){C.Window.ShowToast(message,color);}
  public override void Dispose(){disposed=true;base.Dispose();}
 }
}
