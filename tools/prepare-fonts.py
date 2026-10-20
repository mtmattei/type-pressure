"""Prepare exact OpenType gvar data for the native renderer. Requires fonttools.

SkiaSharp 3 (used by Uno 6) does not expose variable-font axis cloning. This
extracts the original font's variation tuples, resolving IUP interpolation
once. The C# renderer applies OpenType support scalars to those tuples at
runtime. No raster samples, horizontal scaling, or synthetic font weights.
"""
import gzip
import json
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.varLib.iup import iup_delta

root = Path(__file__).resolve().parents[1] / 'TypePressure' / 'Assets' / 'Fonts'
for name in ['RobotoFlex', 'Anybody']:
    font = TTFont(root / f'{name}.ttf')
    pinned = {a.axisTag: (144 if a.axisTag == 'opsz' else a.defaultValue)
              for a in font['fvar'].axes if a.axisTag not in ['wght', 'wdth', 'slnt']}
    if pinned:
        font = instantiateVariableFont(font, pinned, inplace=True)
    axes = [{'tag': a.axisTag, 'min': a.minValue, 'default': a.defaultValue,
             'max': a.maxValue, 'map': sorted(font['avar'].segments[a.axisTag].items()) if 'avar' in font else [[-1,-1],[0,0],[1,1]]}
            for a in font['fvar'].axes]
    glyphs = {}
    for gn in font.getGlyphOrder():
        coords, ctrl = font['glyf']._getCoordinatesAndControls(gn, font['hmtx'].metrics)
        variations = []
        for variation in font['gvar'].variations.get(gn, []):
            delta = variation.coordinates
            if any(d is None for d in delta):
                delta = iup_delta(delta, coords, ctrl.endPts)
            variations.append({'support': variation.axes, 'delta': [[round(x,5),round(y,5)] for x,y in delta]})
        components = []
        if ctrl.components:
            for component in font['glyf'][gn].components:
                component_name, transform = component.getComponentInfo()
                components.append({'glyph':component_name, 'matrix':list(transform[:4])})
        glyphs[gn] = {'points':list(coords), 'ends':list(ctrl.endPts) if not components else [],
                      'flags':[int(f)&1 for f in ctrl.flags] if ctrl.flags else [],
                      'components':components, 'variations':variations}
    data = {'name': name, 'units': font['head'].unitsPerEm, 'axes':axes,
            'cmap':{str(k):v for k,v in font.getBestCmap().items()}, 'glyphs':glyphs}
    target=root / f'{name}.json.gz'
    target.write_bytes(gzip.compress(json.dumps(data,separators=(',',':')).encode(),mtime=0))
    # Independent expected coordinates for regression checks of native interpolation.
    checks=[]
    for location in [{'wght':100,'wdth':50,'slnt':0},{'wght':720,'wdth':121,'slnt':-6},{'wght':900,'wdth':75,'slnt':-10}]:
        valid={a['tag']:max(a['min'],min(a['max'],location[a['tag']])) for a in axes}
        instance=instantiateVariableFont(font,valid,inplace=False)
        for ch in ['A','P','R','S','g','é','æ','@']:
            gn=instance.getBestCmap().get(ord(ch))
            if not gn:continue
            coords,_=instance['glyf']._getCoordinatesAndControls(gn,instance['hmtx'].metrics)
            checks.append({'character':ch,'location':valid,'points':list(coords)})
    (root / f'{name}.checks.json').write_text(json.dumps(checks,separators=(',',':')),encoding='utf-8')
    print(f'{name}: {len(glyphs)} glyphs, {len(axes)} live axes, {target.stat().st_size:,} bytes')
