"""Render every 100 ms frame with the actual browser canvas. Internal QA only."""
import base64,json,pathlib
from playwright.sync_api import sync_playwright
root=pathlib.Path(__file__).resolve().parents[2]
out=root/"Reports"/"PlayerViewFrames"
out.mkdir(parents=True,exist_ok=True)
with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,args=["--no-sandbox"])
    page=browser.new_page(viewport={"width":1500,"height":900},device_scale_factor=1)
    page.on("pageerror",lambda e: print("PAGE ERROR",e))
    page.goto((root/"Tools/ChartRework/full_song_visual_review.html").as_uri())
    all_stats=[]
    for i in range(6):
        r=page.evaluate("(i)=>window.auditSong(i)",i)
        print(i,r["stats"],flush=True)
        all_stats.append(r["stats"])
        for k,s in enumerate(r["sheets"]):
            (out/f"song_{i}_{k:02d}.jpg").write_bytes(base64.b64decode(s.split(",",1)[1]))
        for j,t in enumerate(r["samples"]):
            for k,dt in enumerate([-.2,-.1,0,.1,.2]):
                page.evaluate("([i,t])=>window.drawAt(i,t)",[i,max(0,t+dt)])
                page.locator("#stage").screenshot(path=str(out/f"critical_{i}_{j}_{k}.png"))
    (out/"summary.json").write_text(json.dumps(all_stats,indent=2))
    browser.close()
