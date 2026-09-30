#requires -Version 7
param([Parameter(Mandatory)][string]$DataDirectory)
$ErrorActionPreference='Stop'
$rrData=(Resolve-Path -LiteralPath $DataDirectory).Path
$rrMeta=Get-Content -LiteralPath (Join-Path $rrData 'live-expansion/frames.json') -Raw | ConvertFrom-Json
$rrCommits=@(Get-Content -LiteralPath (Join-Path $rrData 'live-commits.json') -Raw | ConvertFrom-Json)
if(!$rrMeta.frames -or !$rrCommits -or $rrMeta.frequency -le 0){throw 'Missing capture witnesses.'}
Add-Type -AssemblyName System.Drawing
if(!('PostureStripeScanner' -as [type])) {
$rrDrawingReferences=@('System.Drawing.Common','System.Drawing.Primitives','System.Collections')
foreach($rrDependency in @('System.Private.Windows.Core','System.Private.Windows.GdiPlus')) {
    if(Test-Path -LiteralPath (Join-Path $PSHOME ($rrDependency+'.dll'))){$rrDrawingReferences+=$rrDependency}
}
Add-Type -ReferencedAssemblies $rrDrawingReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Collections.Generic;
public static class PostureStripeScanner {
    // Wide neutral-white rows only. This heuristic is NOT a visual acceptance oracle:
    // wallpapers/occluders can match, and faint/short/missed stripes can evade it.
    public static int[] Rows(string path) {
        using var b = new Bitmap(path);
        var hits = new List<int>();
        for(int y=2;y<b.Height-2;y++) {
            int white=0, samples=0;
            for(int x=12;x<b.Width-12;x+=4) {
                var c=b.GetPixel(x,y); samples++;
                if(c.A>=245 && Math.Min(c.R,Math.Min(c.G,c.B))>=235 &&
                    Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<=16) white++;
            }
            if(samples>0 && white>=samples*.9) hits.Add(y);
        }
        return hits.ToArray();
    }
}
'@
}
$rrRows=@(foreach($rrFrame in $rrMeta.frames){
    $rrCommit=$rrCommits | Where-Object qpc -le $rrFrame.Qpc | Select-Object -Last 1
    foreach($rrPlane in $(if($rrMeta.screenOnly){@('screen')}else{@('frame','screen')})){
        $rrFile=$rrFrame.file -replace '^frame',$rrPlane
        $rrPath=Join-Path $rrData ('live-expansion/'+$rrFile)
        if(Test-Path -LiteralPath $rrPath){
            $rrWhite=@([PostureStripeScanner]::Rows($rrPath))
            [pscustomobject]@{File=$rrFile;Plane=$rrPlane;Qpc=$rrFrame.Qpc;
                AcceptedFrame=$rrCommit.frame.AcceptedFrame;VisibleHeight=$rrCommit.frame.VisibleBounds.Height;
                CommitToCaptureMs=if($rrCommit){($rrFrame.Qpc-$rrCommit.qpc)*1000/$rrMeta.frequency}else{$null};
                WhiteRows=$rrWhite}
        }
    }
})
$rrReport=[pscustomobject]@{VisualAccepted=$false;Scope='Heuristic wide white rows; QPC association approximate, screen crop follows GPU copy; inspect occlusion and pixels manually';
    Frames=$rrMeta.frames.Count;ScreenFrames=@($rrRows | Where-Object Plane -eq screen).Count;
    CandidateRows=@($rrRows | Where-Object {$_.WhiteRows.Count -gt 0});
    CommitCount=$rrCommits.Count;DurationMs=($rrCommits[-1].qpc-$rrCommits[0].qpc)*1000/$rrMeta.frequency;
    CanvasIdentityCount=@($rrCommits.layout.contentIdentity | Sort-Object -Unique).Count;
    StableStructureSizes=@($rrCommits | ForEach-Object {'{0}x{1}' -f $_.layout.structureWidth,$_.layout.structureHeight} | Sort-Object -Unique);
    FramesDetail=$rrRows}
$rrOutput=Join-Path $rrData 'stripe-analysis.json'
if(Test-Path -LiteralPath $rrOutput){throw 'Preserve prior analysis; do not overwrite.'}
$rrReport | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $rrOutput -Encoding utf8
$rrReport | Select-Object VisualAccepted,Frames,ScreenFrames,CommitCount,DurationMs,CanvasIdentityCount,StableStructureSizes,@{Name='CandidateImages';Expression={$_.CandidateRows.Count}} | ConvertTo-Json
