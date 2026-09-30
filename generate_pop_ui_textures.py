import os
from PIL import Image, ImageDraw, ImageFilter

def create_pop_card(w, h, dst_path):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    r = 20
    border_col = (25, 35, 60, 255)       # コミック風ダークネイビー枠
    bg_top = (255, 255, 255, 240)        # クリーンホワイト
    bg_bot = (235, 246, 255, 245)        # ほんのりパステルスカイブルー
    
    # ドロップシャドウ
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.rounded_rectangle([4, 6, w-4, h-2], radius=r, fill=(10, 15, 30, 80))
    shadow = shadow.filter(ImageFilter.GaussianBlur(3))
    img.paste(shadow, (0, 0), shadow)
    
    # 外枠（太いコミック枠）
    draw.rounded_rectangle([2, 2, w-4, h-4], radius=r, fill=border_col)
    
    # 本体（白〜パステルブルーグラデーション）
    body = Image.new("RGBA", (w-8, h-8), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(body)
    for y in range(h-8):
        t = y / float(h-8)
        c = (
            int(bg_top[0] * (1-t) + bg_bot[0] * t),
            int(bg_top[1] * (1-t) + bg_bot[1] * t),
            int(bg_top[2] * (1-t) + bg_bot[2] * t),
            248
        )
        b_draw.line([(0, y), (w-8, y)], fill=c)
        
    mask = Image.new("L", (w-8, h-8), 0)
    m_draw = ImageDraw.Draw(mask)
    m_draw.rounded_rectangle([0, 0, w-9, h-9], radius=r-3, fill=255)
    img.paste(body, (4, 4), mask)
    
    # 内側細枠線（アクセント）
    draw.rounded_rectangle([7, 7, w-8, h-8], radius=r-5, outline=(215, 235, 255, 200), width=2)
    
    os.makedirs(os.path.dirname(dst_path), exist_ok=True)
    img.save(dst_path, "PNG")
    print(f"Saved Pop Card: {dst_path}")

def create_pop_ribbon(w, h, top_c, bot_c, dst_path):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    r = h // 2
    
    # 外枠
    draw.rounded_rectangle([1, 1, w-2, h-2], radius=r, fill=(20, 25, 45, 255))
    
    body = Image.new("RGBA", (w-4, h-4), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(body)
    for y in range(h-4):
        t = y / float(h-4)
        c = (
            int(top_c[0] * (1-t) + bot_c[0] * t),
            int(top_c[1] * (1-t) + bot_c[1] * t),
            int(top_c[2] * (1-t) + bot_c[2] * t),
            255
        )
        b_draw.line([(0, y), (w-4, y)], fill=c)
        
    mask = Image.new("L", (w-4, h-4), 0)
    m_draw = ImageDraw.Draw(mask)
    m_draw.rounded_rectangle([0, 0, w-5, h-5], radius=r-2, fill=255)
    img.paste(body, (2, 2), mask)
    
    # 上部ハイライト
    highlight = Image.new("RGBA", (w-8, (h-4)//2), (0, 0, 0, 0))
    h_draw = ImageDraw.Draw(highlight)
    h_draw.rounded_rectangle([0, 0, w-9, (h-4)//2 - 1], radius=(h-4)//4, fill=(255, 255, 255, 120))
    img.paste(highlight, (4, 3), highlight)
    
    os.makedirs(os.path.dirname(dst_path), exist_ok=True)
    img.save(dst_path, "PNG")
    print(f"Saved Pop Ribbon: {dst_path}")

def create_pop_button(w, h, dst_path):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    r = h // 2
    
    # ドロップシャドウ
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.rounded_rectangle([2, 5, w-2, h-1], radius=r, fill=(20, 30, 60, 110))
    shadow = shadow.filter(ImageFilter.GaussianBlur(2))
    img.paste(shadow, (0, 0), shadow)
    
    # 外枠
    draw.rounded_rectangle([2, 1, w-3, h-3], radius=r, fill=(20, 25, 45, 255))
    
    top_c = (255, 240, 45, 255)
    bot_c = (255, 135, 15, 255)
    
    body = Image.new("RGBA", (w-6, h-6), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(body)
    for y in range(h-6):
        t = y / float(h-6)
        c = (
            int(top_c[0] * (1-t) + bot_c[0] * t),
            int(top_c[1] * (1-t) + bot_c[1] * t),
            int(top_c[2] * (1-t) + bot_c[2] * t),
            255
        )
        b_draw.line([(0, y), (w-6, y)], fill=c)
        
    mask = Image.new("L", (w-6, h-6), 0)
    m_draw = ImageDraw.Draw(mask)
    m_draw.rounded_rectangle([0, 0, w-7, h-7], radius=r-3, fill=255)
    img.paste(body, (3, 3), mask)
    
    # ハイライト
    highlight = Image.new("RGBA", (w-12, (h-6)//2), (0, 0, 0, 0))
    h_draw = ImageDraw.Draw(highlight)
    h_draw.rounded_rectangle([0, 0, w-13, (h-6)//2 - 1], radius=(h-6)//4, fill=(255, 255, 255, 150))
    img.paste(highlight, (6, 5), highlight)
    
    os.makedirs(os.path.dirname(dst_path), exist_ok=True)
    img.save(dst_path, "PNG")
    print(f"Saved Pop Button: {dst_path}")

res_dir = r"c:\Users\kaito\UnityProjects\TurboCircuit\Assets\Resources\UI"
create_pop_card(340, 420, os.path.join(res_dir, "ui_card_pop.png"))
create_pop_button(450, 52, os.path.join(res_dir, "ui_btn_race.png"))
create_pop_ribbon(300, 32, (60, 200, 255), (10, 130, 240), os.path.join(res_dir, "ui_ribbon_track.png"))
create_pop_ribbon(300, 32, (255, 175, 40), (255, 105, 15), os.path.join(res_dir, "ui_ribbon_driver.png"))
