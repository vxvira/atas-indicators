using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
var src = args[0]; var dst = args[1];
var asm = AssemblyDefinition.ReadAssembly(src);
var mod = asm.MainModule;
TypeDefinition T(string n) => mod.Types.First(t => t.FullName == n);
var ind = T("FootprintX.FootprintIndicator");
var set = T("FootprintX.FootprintSettings");
var calc = T("FootprintX.FootprintCalc");
var bar = T("FootprintX.BarModel");
var row = T("FootprintX.FootprintRow");
var rend = T("FootprintX.FootprintRenderer");
var modeEnum = T("FootprintX.FootprintMode");
if (set.Fields.Any(f => f.Name == "HeatOn")) throw new Exception("already patched");

TypeReference Ref(string full) => mod.GetTypeReferences().First(t => t.FullName == full);
var tDec = Ref("System.Decimal"); var tColor = Ref("System.Drawing.Color"); var tBool = mod.TypeSystem.Boolean;
var tInt = mod.TypeSystem.Int32; var tDbl = mod.TypeSystem.Double; var tVoid = mod.TypeSystem.Void;
FieldDefinition SF(string n) => set.Fields.First(f => f.Name == n);
FieldDefinition RF(string n) => row.Fields.First(f => f.Name == n);

modeEnum.Fields.Add(new FieldDefinition("BidAskHeatCells", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, modeEnum) { Constant = 7 });

var fOn = new FieldDefinition("HeatOn", FieldAttributes.Public, tBool);
var fThr = new FieldDefinition("GrayThr", FieldAttributes.Public, tDec);
var fCol = new FieldDefinition("GrayCol", FieldAttributes.Public, tColor);
set.Fields.Add(fOn); set.Fields.Add(fThr); set.Fields.Add(fCol);
var fHeat = new FieldDefinition("Heat", FieldAttributes.Public, tBool);
bar.Fields.Add(fHeat);
var fHL = new FieldDefinition("HeatL", FieldAttributes.Public, mod.TypeSystem.String);
var fHR = new FieldDefinition("HeatR", FieldAttributes.Public, mod.TypeSystem.String);
row.Fields.Add(fHL); row.Fields.Add(fHR);

MethodReference MR(TypeReference decl, string name, TypeReference ret, bool hasThis, params TypeReference[] ps) {
    var m = new MethodReference(name, ret, decl) { HasThis = hasThis };
    foreach (var p in ps) m.Parameters.Add(new ParameterDefinition(p));
    return m;
}
var opLt = MR(tDec, "op_LessThan", tBool, false, tDec, tDec);
var opDiv = MR(tDec, "op_Division", tDec, false, tDec, tDec);
var opSub = MR(tDec, "op_Subtraction", tDec, false, tDec, tDec);
var opGt = MR(tDec, "op_GreaterThan", tBool, false, tDec, tDec);
var fmtVol = calc.Methods.First(m => m.Name == "FormatVolume");
var toDouble = MR(tDec, "ToDouble", tDbl, false, tDec);
var fromArgb4 = MR(tColor, "FromArgb", tColor, false, tInt, tInt, tInt, tInt);
var decCtorInt = MR(tDec, ".ctor", tVoid, true, tInt);
var tMath = Ref("System.Math");
var mathMax = MR(tMath, "Max", tDec, false, tDec, tDec);
var decZero = new FieldReference("Zero", tDec, tDec);
var decOne = new FieldReference("One", tDec, tDec);
var lerp = calc.Methods.First(m => m.Name == "Lerp");

var hc = new MethodDefinition("HC", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, tColor);
hc.Parameters.Add(new ParameterDefinition("gate", ParameterAttributes.None, tDec));
hc.Parameters.Add(new ParameterDefinition("d", ParameterAttributes.None, tDec));
hc.Parameters.Add(new ParameterDefinition("max", ParameterAttributes.None, tDec));
hc.Parameters.Add(new ParameterDefinition("s", ParameterAttributes.None, set));
hc.Parameters.Add(new ParameterDefinition("ask", ParameterAttributes.None, tInt));
{
    var il = hc.Body.GetILProcessor();
    var l1 = il.Create(OpCodes.Ldarg, hc.Parameters[4]); var askL = il.Create(OpCodes.Ldc_I4, 255); var join = il.Create(OpCodes.Ldarg_1);
    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_3); il.Emit(OpCodes.Ldfld, fThr); il.Emit(OpCodes.Call, opLt);
    var cont = il.Create(OpCodes.Nop);
    il.Emit(OpCodes.Brfalse, cont);
    il.Emit(OpCodes.Ldarg_3); il.Emit(OpCodes.Ldfld, fCol); il.Emit(OpCodes.Ret);
    il.Append(cont);
    il.Append(l1); il.Emit(OpCodes.Brtrue, askL);
    foreach (var v in new[] { 255, 170, 140, 215 }) il.Emit(OpCodes.Ldc_I4, v); il.Emit(OpCodes.Call, fromArgb4);
    foreach (var v in new[] { 255, 215, 80, 205 }) il.Emit(OpCodes.Ldc_I4, v); il.Emit(OpCodes.Call, fromArgb4);
    il.Emit(OpCodes.Br, join);
    il.Append(askL); foreach (var v in new[] { 140, 205, 150 }) il.Emit(OpCodes.Ldc_I4, v); il.Emit(OpCodes.Call, fromArgb4);
    foreach (var v in new[] { 255, 60, 215, 185 }) il.Emit(OpCodes.Ldc_I4, v); il.Emit(OpCodes.Call, fromArgb4);
    il.Append(join);
    il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Call, opDiv); il.Emit(OpCodes.Call, toDouble); il.Emit(OpCodes.Call, lerp); il.Emit(OpCodes.Ret);
}
calc.Methods.Add(hc);

var helper = new MethodDefinition("HeatCells", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, tVoid);
helper.Parameters.Add(new ParameterDefinition("m", ParameterAttributes.None, bar));
helper.Parameters.Add(new ParameterDefinition("s", ParameterAttributes.None, set));
helper.Body.InitLocals = true;
{
    var il = helper.Body.GetILProcessor();
    var vRows = new VariableDefinition(new ArrayType(row)); var vI = new VariableDefinition(tInt); var vR = new VariableDefinition(new ByReferenceType(row)); var vMax = new VariableDefinition(tDec);
    helper.Body.Variables.Add(vRows); helper.Body.Variables.Add(vI); helper.Body.Variables.Add(vR); helper.Body.Variables.Add(vMax);
    var end = il.Create(OpCodes.Ret);
    il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldfld, fOn); il.Emit(OpCodes.Brfalse, end);
    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Stfld, fHeat);
    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, bar.Fields.First(f => f.Name == "Rows")); il.Emit(OpCodes.Stloc, vRows);
    il.Emit(OpCodes.Ldloc, vRows); il.Emit(OpCodes.Brfalse, end);
    il.Emit(OpCodes.Ldsfld, decOne); il.Emit(OpCodes.Stloc, vMax);
    il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stloc, vI);
    var c1 = il.Create(OpCodes.Ldloc, vI); il.Emit(OpCodes.Br, c1);
    var b1 = il.Create(OpCodes.Ldloc, vMax); il.Append(b1);
    il.Emit(OpCodes.Ldloc, vRows); il.Emit(OpCodes.Ldloc, vI); il.Emit(OpCodes.Ldelema, row); il.Emit(OpCodes.Ldfld, RF("Ask"));
    il.Emit(OpCodes.Ldloc, vRows); il.Emit(OpCodes.Ldloc, vI); il.Emit(OpCodes.Ldelema, row); il.Emit(OpCodes.Ldfld, RF("Bid"));
    il.Emit(OpCodes.Call, opSub);
    il.Emit(OpCodes.Ldloc, vRows); il.Emit(OpCodes.Ldloc, vI); il.Emit(OpCodes.Ldelema, row); il.Emit(OpCodes.Ldfld, RF("Bid"));
    il.Emit(OpCodes.Ldloc, vRows); il.Emit(OpCodes.Ldloc, vI); il.Emit(OpCodes.Ldelema, row); il.Emit(OpCodes.Ldfld, RF("Ask"));
    il.Emit(OpCodes.Call, opSub);
    il.Emit(OpCodes.Call, mathMax); il.Emit(OpCodes.Call, mathMax); il.Emit(OpCodes.Stloc, vMax);
    il.Emit(OpCodes.Ldloc, vI); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc, vI);
    il.Append(c1); il.Emit(OpCodes.Ldloc, vRows); il.Emit(OpCodes.Ldlen); il.Emit(OpCodes.Conv_I4); il.Emit(OpCodes.Blt, b1);
    il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stloc, vI);
    var c2 = il.Create(OpCodes.Ldloc, vI); il.Emit(OpCodes.Br, c2);
    var b2 = il.Create(OpCodes.Ldloc, vRows); il.Append(b2);
    il.Emit(OpCodes.Ldloc, vI); il.Emit(OpCodes.Ldelema, row); il.Emit(OpCodes.Stloc, vR);
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldsfld, decZero); il.Emit(OpCodes.Stfld, RF("BarValue"));
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldsfld, decZero); il.Emit(OpCodes.Stfld, RF("BarValue2"));
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldflda, RF("CellFill")); il.Emit(OpCodes.Initobj, tColor);
    foreach (var f in new[] { "BarColor", "BarColor2" }) { il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldfld, fCol); il.Emit(OpCodes.Stfld, RF(f)); }
    var notAsk = il.Create(OpCodes.Nop); var rowEnd = il.Create(OpCodes.Nop);
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Ask")); il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Bid")); il.Emit(OpCodes.Call, opGt); il.Emit(OpCodes.Brfalse, notAsk);
    il.Emit(OpCodes.Ldloc, vR);
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Ask"));
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Ask")); il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Bid")); il.Emit(OpCodes.Call, opSub);
    il.Emit(OpCodes.Ldloc, vMax); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Call, hc); il.Emit(OpCodes.Stfld, RF("BarColor2"));
    il.Emit(OpCodes.Br, rowEnd);
    il.Append(notAsk);
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Bid")); il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Ask")); il.Emit(OpCodes.Call, opGt); il.Emit(OpCodes.Brfalse, rowEnd);
    il.Emit(OpCodes.Ldloc, vR);
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Bid"));
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Bid")); il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Ask")); il.Emit(OpCodes.Call, opSub);
    il.Emit(OpCodes.Ldloc, vMax); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Call, hc); il.Emit(OpCodes.Stfld, RF("BarColor"));
    il.Append(rowEnd);
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Bid")); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldfld, SF("AbbreviateK")); il.Emit(OpCodes.Call, fmtVol); il.Emit(OpCodes.Stfld, fHL);
    il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldfld, RF("Ask")); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldfld, SF("AbbreviateK")); il.Emit(OpCodes.Call, fmtVol); il.Emit(OpCodes.Stfld, fHR);
    foreach (var f in new[] { "LeftText", "RightText" }) { il.Emit(OpCodes.Ldloc, vR); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Stfld, RF(f)); }
    foreach (var f in new[] { "LeftTextColor", "RightTextColor" }) {
        il.Emit(OpCodes.Ldloc, vR);
        foreach (var v in new[] { 255, 22, 22, 28 }) il.Emit(OpCodes.Ldc_I4, v);
        il.Emit(OpCodes.Call, fromArgb4); il.Emit(OpCodes.Stfld, RF(f));
    }
    il.Emit(OpCodes.Ldloc, vI); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc, vI);
    il.Append(c2); il.Emit(OpCodes.Ldloc, vRows); il.Emit(OpCodes.Ldlen); il.Emit(OpCodes.Conv_I4); il.Emit(OpCodes.Blt, b2);
    il.Append(end);
}
calc.Methods.Add(helper);

{
    var build = calc.Methods.First(m => m.Name == "Build");
    var ins = build.Body.Instructions;
    if (ins.Count(i => i.OpCode == OpCodes.Ret) != 1) throw new Exception("Build has multiple rets");
    var ld = ins[ins.Count - 2];
    if (!(ld.OpCode.Code == Code.Ldloc_S || ld.OpCode.Code == Code.Ldloc)) throw new Exception("unexpected tail " + ld);
    var bp = build.Body.GetILProcessor(); var lv = (VariableDefinition)ld.Operand;
    bp.InsertBefore(ld, bp.Create(OpCodes.Ldloc, lv));
    bp.InsertBefore(ld, bp.Create(OpCodes.Ldarg_1));
    bp.InsertBefore(ld, bp.Create(OpCodes.Call, helper));
}

var drawBar = rend.Methods.First(m => m.Name == "DrawBar");
MethodReference fillRect = null, rectCtor = null; TypeReference rcType = null;
foreach (var i in drawBar.Body.Instructions) {
    if (i.Operand is MethodReference mr) {
        if (mr.Name == "FillRectangle" && fillRect == null) fillRect = mr;
        if (mr.Name == ".ctor" && mr.DeclaringType.Name == "Rectangle" && rectCtor == null) rectCtor = mr;
    }
}
rcType = drawBar.Parameters[0].ParameterType;
var tHelper = new MethodDefinition("DrawHeat", MethodAttributes.Public | MethodAttributes.HideBySig, tVoid) { HasThis = true };
var rOptT = drawBar.Parameters.First(q => q.ParameterType.Name == "RenderOptions").ParameterType;
tHelper.Parameters.Add(new ParameterDefinition("c", ParameterAttributes.None, rcType));
tHelper.Parameters.Add(new ParameterDefinition("r", ParameterAttributes.None, new ByReferenceType(row)));
foreach (var n in new[] { "x", "y", "hw", "h" }) tHelper.Parameters.Add(new ParameterDefinition(n, ParameterAttributes.None, tInt));
tHelper.Parameters.Add(new ParameterDefinition("m", ParameterAttributes.None, bar));
tHelper.Parameters.Add(new ParameterDefinition("o", ParameterAttributes.None, rOptT));
{
    var P = tHelper.Parameters;
    var il = tHelper.Body.GetILProcessor(); var end = il.Create(OpCodes.Ret);
    bool ret = fillRect.ReturnType.FullName != "System.Void";
    var textM = rend.Methods.First(m => m.Name == "Text");
    var textFits = rend.Methods.First(m => m.Name == "TextFits");
    var fCenter = rend.Fields.First(f => f.Name == "_center");
    var fFont = T("FootprintX.RenderOptions").Fields.First(f => f.Name == "Font");
    il.Emit(OpCodes.Ldarg, P[6]); il.Emit(OpCodes.Ldfld, fHeat); il.Emit(OpCodes.Brfalse, end);
    void Rect(bool right) {
        il.Emit(OpCodes.Ldarg, P[2]); if (right) { il.Emit(OpCodes.Ldarg, P[4]); il.Emit(OpCodes.Add); }
        il.Emit(OpCodes.Ldarg, P[3]); il.Emit(OpCodes.Ldarg, P[4]); il.Emit(OpCodes.Ldarg, P[5]);
        il.Emit(OpCodes.Newobj, rectCtor);
    }
    foreach (var right in new[] { false, true }) {
        il.Emit(OpCodes.Ldarg, P[0]); il.Emit(OpCodes.Ldarg, P[1]); il.Emit(OpCodes.Ldfld, RF(right ? "BarColor2" : "BarColor"));
        Rect(right); il.Emit(OpCodes.Callvirt, fillRect); if (ret) il.Emit(OpCodes.Pop);
    }
    il.Emit(OpCodes.Ldarg, P[7]); il.Emit(OpCodes.Ldarg, P[5]); il.Emit(OpCodes.Call, textFits); il.Emit(OpCodes.Brfalse, end);
    foreach (var right in new[] { false, true }) {
        il.Emit(OpCodes.Ldarg, P[0]); il.Emit(OpCodes.Ldarg, P[1]); il.Emit(OpCodes.Ldfld, right ? fHR : fHL);
        il.Emit(OpCodes.Ldarg, P[7]); il.Emit(OpCodes.Ldfld, fFont);
        il.Emit(OpCodes.Ldarg, P[1]); il.Emit(OpCodes.Ldfld, RF(right ? "RightTextColor" : "LeftTextColor"));
        Rect(right);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fCenter);
        il.Emit(OpCodes.Ldarg, P[7]); il.Emit(OpCodes.Call, textM);
    }
    il.Append(end);
}
rend.Methods.Add(tHelper);
{
    var ins = drawBar.Body.Instructions;
    Instruction at = null;
    for (int i = 0; i < ins.Count - 1; i++)
        if (ins[i + 1].OpCode == OpCodes.Ldflda && ins[i + 1].Operand is FieldReference f && f.Name == "CellFill") { at = ins[i]; break; }
    if (at == null) throw new Exception("CellFill site not found");
    var v24 = (VariableDefinition)at.Operand;
    var vX = drawBar.Body.Variables[12]; var vY = drawBar.Body.Variables[25]; var vHW = drawBar.Body.Variables[2]; var vH = drawBar.Body.Variables[4];
    var p = drawBar.Body.GetILProcessor();
    var first = p.Create(OpCodes.Ldarg_0);
    var seq = new[] { first, p.Create(OpCodes.Ldarg_1), p.Create(OpCodes.Ldloc, v24), p.Create(OpCodes.Ldloc, vX), p.Create(OpCodes.Ldloc, vY), p.Create(OpCodes.Ldloc, vHW), p.Create(OpCodes.Ldloc, vH), p.Create(OpCodes.Ldarg, drawBar.Parameters.First(q => q.ParameterType.Name == "BarModel")), p.Create(OpCodes.Ldarg, drawBar.Parameters.First(q => q.ParameterType.Name == "RenderOptions")), p.Create(OpCodes.Callvirt, tHelper) };
    foreach (var s in seq) p.InsertBefore(at, s);
    foreach (var i in ins) { if (i.Operand == at && !seq.Contains(i)) i.Operand = first; }
}

{
    var fCalcI = ind.Fields.First(f => f.Name == "_calc"); var fRo = ind.Fields.First(f => f.Name == "_ro");
    var changed = ind.Methods.First(m => m.Name == "Changed");
    var p = ind.Properties.First(x => x.Name == "Mode");
    var g = p.GetMethod.Body; g.Instructions.Clear(); var gi = g.GetILProcessor();
    var plain = gi.Create(OpCodes.Ldarg_0);
    gi.Emit(OpCodes.Ldarg_0); gi.Emit(OpCodes.Ldfld, fCalcI); gi.Emit(OpCodes.Ldfld, fOn); gi.Emit(OpCodes.Brfalse, plain);
    gi.Emit(OpCodes.Ldc_I4_7); gi.Emit(OpCodes.Ret);
    gi.Append(plain); gi.Emit(OpCodes.Ldfld, fCalcI); gi.Emit(OpCodes.Ldfld, SF("Mode")); gi.Emit(OpCodes.Ret);
    var s = p.SetMethod.Body; s.Instructions.Clear(); var si = s.GetILProcessor();
    var notHeat = si.Create(OpCodes.Ldarg_0); var fin = si.Create(OpCodes.Ldarg_0);
    si.Emit(OpCodes.Ldarg_1); si.Emit(OpCodes.Ldc_I4_7); si.Emit(OpCodes.Bne_Un, notHeat);
    si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldfld, fCalcI); si.Emit(OpCodes.Ldc_I4_1); si.Emit(OpCodes.Stfld, fOn);
    si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldfld, fCalcI); si.Emit(OpCodes.Ldc_I4_1); si.Emit(OpCodes.Stfld, SF("Mode"));
    si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldfld, fCalcI); si.Emit(OpCodes.Ldc_I4_3); si.Emit(OpCodes.Stfld, SF("LeftValue"));
    si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldfld, fCalcI); si.Emit(OpCodes.Ldc_I4_4); si.Emit(OpCodes.Stfld, SF("RightValue"));
    si.Emit(OpCodes.Br, fin);
    si.Append(notHeat); si.Emit(OpCodes.Ldfld, fCalcI); si.Emit(OpCodes.Ldc_I4_0); si.Emit(OpCodes.Stfld, fOn);
    si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldfld, fCalcI); si.Emit(OpCodes.Ldarg_1); si.Emit(OpCodes.Stfld, SF("Mode"));
    si.Append(fin); si.Emit(OpCodes.Ldstr, "Mode"); si.Emit(OpCodes.Ldc_I4_1); si.Emit(OpCodes.Call, changed); si.Emit(OpCodes.Ret);
}

var fCalc = ind.Fields.First(f => f.Name == "_calc");
{
    var ctor = ind.Methods.First(m => m.IsConstructor && !m.IsStatic);
    var cp = ctor.Body.GetILProcessor(); var cr = ctor.Body.Instructions.Last();
    void Ci(Instruction x) => cp.InsertBefore(cr, x);
    Ci(cp.Create(OpCodes.Ldarg_0)); Ci(cp.Create(OpCodes.Ldfld, fCalc)); Ci(cp.Create(OpCodes.Ldc_I4, 10)); Ci(cp.Create(OpCodes.Newobj, decCtorInt)); Ci(cp.Create(OpCodes.Stfld, fThr));
    Ci(cp.Create(OpCodes.Ldarg_0)); Ci(cp.Create(OpCodes.Ldfld, fCalc));
    foreach (var v in new[] { 255, 150, 150, 156 }) Ci(cp.Create(OpCodes.Ldc_I4, v));
    Ci(cp.Create(OpCodes.Call, fromArgb4)); Ci(cp.Create(OpCodes.Stfld, fCol));
}

var refProp = ind.Properties.First(p => p.Name == "MinImbalanceVolume");
var dispRef = refProp.CustomAttributes.First(a => a.AttributeType.Name == "DisplayAttribute");
var changed2 = ind.Methods.First(m => m.Name == "Changed");
var toM = ind.Methods.First(m => m.Name == "To"); var fromM = ind.Methods.First(m => m.Name == "From");
void AddProp(string name, TypeReference type, FieldDefinition field, string title, string desc, int order, bool color) {
    var attrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
    var g = new MethodDefinition("get_" + name, attrs, type); var s = new MethodDefinition("set_" + name, attrs, tVoid);
    s.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, type));
    var gi = g.Body.GetILProcessor(); gi.Emit(OpCodes.Ldarg_0); gi.Emit(OpCodes.Ldfld, fCalc); gi.Emit(OpCodes.Ldfld, field);
    if (color) gi.Emit(OpCodes.Call, fromM);
    gi.Emit(OpCodes.Ret);
    var si = s.Body.GetILProcessor(); si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldfld, fCalc); si.Emit(OpCodes.Ldarg_1);
    if (color) si.Emit(OpCodes.Call, toM);
    si.Emit(OpCodes.Stfld, field);
    si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldstr, name); si.Emit(OpCodes.Ldc_I4_1); si.Emit(OpCodes.Call, changed2); si.Emit(OpCodes.Ret);
    ind.Methods.Add(g); ind.Methods.Add(s);
    var p = new PropertyDefinition(name, PropertyAttributes.None, type) { GetMethod = g, SetMethod = s };
    var d = new CustomAttribute(dispRef.Constructor);
    d.Properties.Add(new CustomAttributeNamedArgument("Name", new CustomAttributeArgument(mod.TypeSystem.String, title)));
    d.Properties.Add(new CustomAttributeNamedArgument("GroupName", new CustomAttributeArgument(mod.TypeSystem.String, "2. Values")));
    d.Properties.Add(new CustomAttributeNamedArgument("Order", new CustomAttributeArgument(mod.TypeSystem.Int32, order)));
    d.Properties.Add(new CustomAttributeNamedArgument("Description", new CustomAttributeArgument(mod.TypeSystem.String, desc)));
    p.CustomAttributes.Add(d);
    ind.Properties.Add(p);
}
AddProp("GrayThreshold", tDec, fThr, "Heat cells: gray below", "BidAskHeatCells mode: a bid or ask cell with volume below this is gray; larger cells shade from pale to strong (bid purple, ask green).", 11, false);
AddProp("GrayColor", tColor, fCol, "Heat cells: gray colour", "BidAskHeatCells mode: colour of cells below the threshold.", 12, true);
{
    var tFloat = mod.TypeSystem.Single;
    var roT = T("FootprintX.RenderOptions");
    var rfT = roT.Fields.First(f => f.Name == "Font").FieldType;
    var fFontR = roT.Fields.First(f => f.Name == "Font"); var fBoldR = roT.Fields.First(f => f.Name == "BoldFont");
    var fAuto = new FieldDefinition("AutoFont", FieldAttributes.Public, tBool);
    var fBaseF = new FieldDefinition("BaseFont", FieldAttributes.Public, rfT);
    var fBaseB = new FieldDefinition("BaseBold", FieldAttributes.Public, rfT);
    var fLast = new FieldDefinition("LastAuto", FieldAttributes.Public, tDec);
    var fDpi = new FieldDefinition("Dpi", FieldAttributes.Public, tDec);
    foreach (var f in new[] { fAuto, fBaseF, fBaseB, fLast, fDpi }) roT.Fields.Add(f);

    var oft = ModuleDefinition.ReadModule(Path.Combine(args.Length > 2 ? args[2] : @"C:\Program Files\ATAS X", "OFT.Platform.Core.dll"));
    var fit = oft.Types.SelectMany(t => t.Methods).First(m => m.Name == "FitToHeight" && m.DeclaringType.Name == "DrawExtensions");
    MethodReference Imp(string n) => mod.ImportReference(fit.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().First(r => r.Name == n));
    var getBold = Imp("get_Bold"); var getFam = Imp("get_FontFamily"); var getStyle = Imp("get_Style");
    var rfCtor3 = mod.ImportReference(fit.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().First(r => r.Name == ".ctor" && r.Parameters.Count == 3 && r.DeclaringType.Name == "RenderFont"));
    var opExplicitSingle = MR(tDec, "op_Explicit", tFloat, false, tDec);
    var opEq = MR(tDec, "op_Equality", tBool, false, tDec, tDec);
    var opMul = MR(tDec, "op_Multiply", tDec, false, tDec, tDec);
    var mathMin = MR(tMath, "Min", tDec, false, tDec, tDec);
    var decImplInt = MR(tDec, "op_Implicit", tDec, false, tInt);
    var decCtor5 = MR(tDec, ".ctor", tVoid, true, tInt, tInt, tInt, tBool, mod.TypeSystem.Byte);
    var opFromDouble = MR(tDec, "op_Explicit", tDec, false, tDbl);

    MethodDefinition Static(string name, TypeReference ret, params (string, TypeReference)[] ps) {
        var m = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, ret);
        foreach (var p in ps) m.Parameters.Add(new ParameterDefinition(p.Item1, ParameterAttributes.None, p.Item2));
        rend.Methods.Add(m); return m;
    }

    var make = Static("MakeFont", rfT, ("b", rfT), ("d", tDec));
    {
        var il = make.Body.GetILProcessor(); var n = il.Create(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Callvirt, getBold); il.Emit(OpCodes.Brfalse, n);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Callvirt, getFam); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, opExplicitSingle); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Newobj, rfCtor3); il.Emit(OpCodes.Ret);
        il.Append(n); il.Emit(OpCodes.Callvirt, getFam); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, opExplicitSingle);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Callvirt, getStyle); il.Emit(OpCodes.Newobj, rfCtor3); il.Emit(OpCodes.Ret);
    }

    var restore = Static("RestoreFonts", tVoid, ("o", roT));
    {
        var il = restore.Body.GetILProcessor(); var end = il.Create(OpCodes.Ret);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fBaseF); il.Emit(OpCodes.Brfalse, end);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fBaseF); il.Emit(OpCodes.Stfld, fFontR);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fBaseB); il.Emit(OpCodes.Stfld, fBoldR);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_M1); il.Emit(OpCodes.Newobj, decCtorInt); il.Emit(OpCodes.Stfld, fLast);
        il.Append(end);
    }

    var autoF = Static("AutoFonts", tVoid, ("o", roT), ("h", tInt));
    {
        var body = autoF.Body; body.InitLocals = true; var vD = new VariableDefinition(tDec); body.Variables.Add(vD);
        var il = body.GetILProcessor();
        var l1 = il.Create(OpCodes.Ldarg_0); var l2 = il.Create(OpCodes.Ldarg_1); var l3 = il.Create(OpCodes.Ldc_I4, 17); var l4 = il.Create(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fAuto); il.Emit(OpCodes.Brtrue, l1);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, restore); il.Emit(OpCodes.Ret);
        il.Append(l1); il.Emit(OpCodes.Ldfld, fBaseF); il.Emit(OpCodes.Brtrue, l2);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fFontR); il.Emit(OpCodes.Stfld, fBaseF);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fBoldR); il.Emit(OpCodes.Stfld, fBaseB);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_M1); il.Emit(OpCodes.Newobj, decCtorInt); il.Emit(OpCodes.Stfld, fLast);
        il.Append(l2); il.Emit(OpCodes.Ldc_I4_3); il.Emit(OpCodes.Bge, l3);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, restore); il.Emit(OpCodes.Ret);
        il.Append(l3); il.Emit(OpCodes.Newobj, decCtorInt);
        il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, decImplInt); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fDpi); il.Emit(OpCodes.Call, opDiv);
        il.Emit(OpCodes.Call, mathMin);
        il.Emit(OpCodes.Ldc_I4, 74); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ldc_I4_2); il.Emit(OpCodes.Newobj, decCtor5);
        il.Emit(OpCodes.Call, opMul); il.Emit(OpCodes.Stloc, vD);
        il.Emit(OpCodes.Ldloc, vD); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fLast); il.Emit(OpCodes.Call, opEq); il.Emit(OpCodes.Brfalse, l4); il.Emit(OpCodes.Ret);
        il.Append(l4); il.Emit(OpCodes.Ldloc, vD); il.Emit(OpCodes.Stfld, fLast);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fBaseF); il.Emit(OpCodes.Ldloc, vD); il.Emit(OpCodes.Call, make); il.Emit(OpCodes.Stfld, fFontR);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, fBaseB); il.Emit(OpCodes.Ldloc, vD); il.Emit(OpCodes.Call, make); il.Emit(OpCodes.Stfld, fBoldR);
        il.Emit(OpCodes.Ret);
    }

    {
        var ins = drawBar.Body.Instructions; var p = drawBar.Body.GetILProcessor();
        int ri = ins.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "RowPx");
        var st = ins.Skip(ri).First(i => i.OpCode.Code == Code.Stloc_3);
        var oPar = drawBar.Parameters.First(q => q.ParameterType.Name == "RenderOptions");
        var seq = new[] { p.Create(OpCodes.Ldarg, oPar), p.Create(OpCodes.Ldloc_3), p.Create(OpCodes.Conv_I4), p.Create(OpCodes.Call, autoF) };
        var after = st.Next;
        foreach (var s in seq) p.InsertBefore(after, s);
    }
    {
        var dl = rend.Methods.First(m => m.Name == "DrawLastPrice"); var p = dl.Body.GetILProcessor(); var first = dl.Body.Instructions[0];
        var oPar = dl.Parameters.First(q => q.ParameterType.Name == "RenderOptions");
        p.InsertBefore(first, p.Create(OpCodes.Ldarg, oPar)); p.InsertBefore(first, p.Create(OpCodes.Call, restore));
    }
    {
        var af = ind.Methods.First(m => m.Name == "ApplyFont"); var p = af.Body.GetILProcessor(); var r = af.Body.Instructions.Last();
        p.InsertBefore(r, p.Create(OpCodes.Ldarg_0)); p.InsertBefore(r, p.Create(OpCodes.Ldfld, ind.Fields.First(f => f.Name == "_ro")));
        p.InsertBefore(r, p.Create(OpCodes.Ldnull)); p.InsertBefore(r, p.Create(OpCodes.Stfld, fBaseF));
    }

    var gdi = new MethodDefinition("GetDpiForSystem", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.PInvokeImpl, mod.TypeSystem.UInt32);
    var u32 = new ModuleReference("user32.dll"); mod.ModuleReferences.Add(u32);
    gdi.PInvokeInfo = new PInvokeInfo(PInvokeAttributes.CallConvWinapi | PInvokeAttributes.NoMangle, "GetDpiForSystem", u32);
    gdi.ImplAttributes |= MethodImplAttributes.PreserveSig;
    rend.Methods.Add(gdi);
    var dpiM = Static("SystemScale", tDec);
    {
        var il = dpiM.Body.GetILProcessor(); var okL = il.Create(OpCodes.Nop);
        il.Emit(OpCodes.Call, gdi); il.Emit(OpCodes.Conv_R8); il.Emit(OpCodes.Ldc_R8, 96.0); il.Emit(OpCodes.Div); il.Emit(OpCodes.Call, opFromDouble);
        il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Newobj, decCtorInt); il.Emit(OpCodes.Call, mathMax); il.Emit(OpCodes.Ret);
    }
    {
        var ctor = ind.Methods.First(m => m.IsConstructor && !m.IsStatic);
        var cp = ctor.Body.GetILProcessor(); var cr = ctor.Body.Instructions.Last(); var fRo = ind.Fields.First(f => f.Name == "_ro");
        cp.InsertBefore(cr, cp.Create(OpCodes.Ldarg_0)); cp.InsertBefore(cr, cp.Create(OpCodes.Ldfld, fRo));
        cp.InsertBefore(cr, cp.Create(OpCodes.Call, dpiM)); cp.InsertBefore(cr, cp.Create(OpCodes.Stfld, fDpi));
        var attrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
        var g = new MethodDefinition("get_AutoFontSize", attrs, tBool); var s = new MethodDefinition("set_AutoFontSize", attrs, tVoid);
        s.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, tBool));
        var gi = g.Body.GetILProcessor(); gi.Emit(OpCodes.Ldarg_0); gi.Emit(OpCodes.Ldfld, fRo); gi.Emit(OpCodes.Ldfld, fAuto); gi.Emit(OpCodes.Ret);
        var si = s.Body.GetILProcessor(); si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldfld, fRo); si.Emit(OpCodes.Ldarg_1); si.Emit(OpCodes.Stfld, fAuto);
        si.Emit(OpCodes.Ldarg_0); si.Emit(OpCodes.Ldstr, "AutoFontSize"); si.Emit(OpCodes.Ldc_I4_0); si.Emit(OpCodes.Call, changed2); si.Emit(OpCodes.Ret);
        ind.Methods.Add(g); ind.Methods.Add(s);
        var p = new PropertyDefinition("AutoFontSize", PropertyAttributes.None, tBool) { GetMethod = g, SetMethod = s };
        var d = new CustomAttribute(dispRef.Constructor);
        d.Properties.Add(new CustomAttributeNamedArgument("Name", new CustomAttributeArgument(mod.TypeSystem.String, "Auto font size")));
        d.Properties.Add(new CustomAttributeNamedArgument("GroupName", new CustomAttributeArgument(mod.TypeSystem.String, "5. Text")));
        d.Properties.Add(new CustomAttributeNamedArgument("Order", new CustomAttributeArgument(mod.TypeSystem.Int32, 11)));
        d.Properties.Add(new CustomAttributeNamedArgument("Description", new CustomAttributeArgument(mod.TypeSystem.String, "Font scales with the row height like the built-in footprint: 0.74 x min(17, row height px / display scale). The Font size is ignored while on.")));
        p.CustomAttributes.Add(d);
        ind.Properties.Add(p);
    }
}
{
    var fNoDec = new FieldDefinition("NoDec", FieldAttributes.Public, tBool);
    set.Fields.Add(fNoDec);
    var sNoDec = new FieldDefinition("NoDecNow", FieldAttributes.Public | FieldAttributes.Static, tBool);
    calc.Fields.Add(sNoDec);
    var tStr = mod.TypeSystem.String;
    var fmtOf = new MethodDefinition("FmtOf", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, tStr);
    {
        var il = fmtOf.Body.GetILProcessor(); var a = il.Create(OpCodes.Ldstr, "0.#");
        il.Emit(OpCodes.Ldsfld, sNoDec); il.Emit(OpCodes.Brfalse, a); il.Emit(OpCodes.Ldstr, "0"); il.Emit(OpCodes.Ret); il.Append(a); il.Emit(OpCodes.Ret);
    }
    calc.Methods.Add(fmtOf);
    var fv = calc.Methods.First(m => m.Name == "FormatVolume");
    var p = fv.Body.GetILProcessor();
    foreach (var i in fv.Body.Instructions.ToList())
        if (i.OpCode == OpCodes.Ldstr && (string)i.Operand == "0.#") p.Replace(i, p.Create(OpCodes.Call, fmtOf));
    var tr = MR(Ref("System.Math"), "Truncate", tDec, false, tDec);
    var first = fv.Body.Instructions[0];
    p.InsertBefore(first, p.Create(OpCodes.Ldsfld, sNoDec));
    p.InsertBefore(first, p.Create(OpCodes.Brfalse, first));
    p.InsertBefore(first, p.Create(OpCodes.Ldarg_0));
    p.InsertBefore(first, p.Create(OpCodes.Call, tr));
    p.InsertBefore(first, p.Create(OpCodes.Starg, fv.Parameters[0]));
    var build = calc.Methods.First(m => m.Name == "Build"); var bp = build.Body.GetILProcessor(); var b0 = build.Body.Instructions[0];
    bp.InsertBefore(b0, bp.Create(OpCodes.Ldarg_1)); bp.InsertBefore(b0, bp.Create(OpCodes.Ldfld, fNoDec)); bp.InsertBefore(b0, bp.Create(OpCodes.Stsfld, sNoDec));
    AddProp("NoDecimals", tBool, fNoDec, "No decimals", "Cell values are truncated to whole numbers (16.78 shows as 16).", 13, false);
}
asm.Write(dst);
Console.WriteLine("patched -> " + dst);
