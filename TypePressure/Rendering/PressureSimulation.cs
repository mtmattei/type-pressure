namespace TypePressure.Rendering;

public enum Experiment { Breathing, Tug, Aftershock, Storm }
public enum ForceKind { Inflate, Compress, Lean, Oscillate }
public sealed class ForceField { public int Id; public ForceKind Kind; public float X; public float Y; }
public sealed record PressureWave(float X,float Y,double Born,float Strength);
public readonly record struct FontAxes(float Weight,float Width,float Slant);

public sealed class PressureSimulation
{
    public const double DemoDuration=56;
    public int DemoScene=>(int)(Time%DemoDuration/8);
    public string Text {get;set;}="PRESSURE";
    public bool Anybody {get;set;}
    public Experiment Mode {get;private set;}
    public float Weight {get;set;}=650;
    public float Width {get;set;}=100;
    public float Slant {get;set;}
    public bool Paused {get;set;}
    public bool Demo {get;set;}
    public double Time {get;private set;}
    public float Squeeze {get;set;}
    public float PointerX,PointerY,PointerVelocity,Pressure;
    public bool PointerDown,PointerInside;
    public int? Selected;
    public List<ForceField> Fields {get;}=[];
    public List<PressureWave> Waves {get;}=[];
    private int _nextId=1;
    public void Step(double dt)
    {
        if(Paused)return;
        dt=Math.Clamp(dt,0,.06);Time+=dt;
        Pressure+=(PointerDown?1.65f-Pressure:-Pressure)*(float)Math.Min(1,dt*4);
        PointerVelocity*=.82f;Waves.RemoveAll(w=>Time-w.Born>3.8);
    }
    public void SetMode(Experiment mode)
    {
        Mode=mode;Time=0;Squeeze=0;Waves.Clear();Fields.Clear();Selected=null;_nextId=1;
        if(mode==Experiment.Tug){AddForce(ForceKind.Inflate,.27f,.51f);AddForce(ForceKind.Compress,.73f,.51f);Selected=null;}
        if(mode==Experiment.Aftershock)Wave(.5f,.5f,1.4f);
    }
    public void Reset(){Weight=650;Width=100;Slant=0;Pressure=0;PointerDown=false;SetMode(Mode);}
    public void RestartClock(){Time=0;Waves.Clear();}
    public ForceField? AddForce(ForceKind kind,float? x=null,float? y=null)
    {
        if(Fields.Count>=6||(Anybody&&kind==ForceKind.Lean))return null;
        var field=new ForceField{Id=_nextId++,Kind=kind,X=x??(.36f+Fields.Count%3*.14f),Y=y??(.48f+Fields.Count%2*.12f)};
        Fields.Add(field);Selected=field.Id;return field;
    }
    public void RemoveSelected(){Fields.RemoveAll(f=>f.Id==Selected);Selected=null;}
    public void Wave(float x,float y,float strength=1){Waves.Add(new(x,y,Time,strength));if(Waves.Count>12)Waves.RemoveAt(0);}
    public FontAxes Axes(int i,int row,int length,float x,float y,float aspect)
    {
        float w=Weight,d=Width,s=Slant;var t=(float)Time;
        if(Demo)
        {
            var elapsed=(float)(Time%DemoDuration);var scene=DemoScene;var phase=(elapsed%8)/8*MathF.Tau+row*.55f;var p=i/(float)Math.Max(1,length-1);
            (w,d,s)=scene switch{
                0=>(550+440*MathF.Sin(phase-MathF.PI/2),100,0),
                1=>(760,88+62*MathF.Sin(phase-MathF.PI/2+p*.6f),0),
                2=>(600,105,-5-5*MathF.Sin(phase-MathF.PI/2+p*1.8f)),
                3=>(550+430*MathF.Sin(phase-p*4),95+47*MathF.Sin(phase-p*4),-4-4*MathF.Cos(phase-p*2)),
                4=>(550+400*MathF.Cos(p*5-phase),95+52*MathF.Sin(p*4+phase),0),
                5=>(550+410*MathF.Sin(phase*2-p*7),100+38*MathF.Sin(phase*2-p*7),-4-4*MathF.Sin(phase*2-p*7)),
                _=>(550+440*MathF.Sin(phase*2+i*.61f+row*1.9f),98+50*MathF.Cos(phase*1.5f+i*.38f+row),-5-5*MathF.Sin(phase*2+row*.7f+i*.23f))};
            return Clamp(w,d,s);
        }
        if(Mode==Experiment.Breathing){w+=MathF.Sin(t*.85f-i*.73f)*230;d+=MathF.Sin(t*.85f-i*.56f)*19;}
        if(Mode==Experiment.Tug)w+=MathF.Sin(t*.6f+i*.35f)*45;
        if(Mode==Experiment.Storm){w+=MathF.Sin(t*1.25f+i*.61f+row*1.9f)*330;d+=MathF.Cos(t*.9f+i*.38f+row)*32;s-=Math.Max(0,MathF.Sin(t+row*.7f+i*.23f))*7;}
        foreach(var f in Fields)
        {
            var dx=x-f.X;var dy=(y-f.Y)*aspect*.8f;var distance=dx*dx+dy*dy;var power=MathF.Exp(-distance/(.3f*.3f*.4f));
            switch(f.Kind){case ForceKind.Inflate:w+=power*520;d+=power*23;break;case ForceKind.Compress:d-=power*75;w-=power*170;break;case ForceKind.Lean:s-=power*10;break;default:var wave=MathF.Sin(t*2.5f-MathF.Sqrt(distance)*12);w+=power*wave*350;d+=power*wave*35;s-=power*(wave+1)*3;break;}
        }
        foreach(var wave in Waves){var age=t-(float)wave.Born;var dx=x-wave.X;var dy=(y-wave.Y)*aspect;var distance=MathF.Sqrt(dx*dx+dy*dy);var front=age*.45f;var power=MathF.Exp(-MathF.Pow((distance-front)/.1f,2))*MathF.Exp(-age*.9f)*wave.Strength;w+=power*400;d+=power*35;s-=power*7;}
        if(PointerInside||Pressure>.01f){var dx=x-PointerX;var dy=(y-PointerY)*aspect*.85f;var power=MathF.Exp(-(dx*dx+dy*dy)/.04f);w+=power*Pressure*450;d+=power*Pressure*26;if(PointerDown)s-=power*Math.Abs(PointerVelocity)*9;}
        d-=Squeeze*76;w+=Squeeze*80;
        return Clamp(w,d,s);
    }
    private FontAxes Clamp(float w,float d,float s)=>new(Math.Clamp(w,100,Anybody?900:1000),Math.Clamp(d,Anybody?50:25,Anybody?150:151),Anybody?0:Math.Clamp(s,-10,0));
}
