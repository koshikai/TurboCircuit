import os
from PIL import Image, ImageEnhance, ImageFilter, ImageOps
import numpy as np

def make_pop_sticker_badge(src_path, dst_path):
    img = Image.open(src_path).convert("RGBA")
    
    # 1. 彩度（Saturation）を上げてポップ＆ジューシーにする
    enhancer = ImageEnhance.Color(img)
    img_vibrant = enhancer.enhance(1.35)
    
    # コントラストも少し上げてアニメ調のメリハリをつける
    enhancer_con = ImageEnhance.Contrast(img_vibrant)
    img_vibrant = enhancer_con.enhance(1.15)
    
    # 2. 白いステッカー風の太い縁取り（Sticker Border）を生成
    # アルファチャンネルを取り出し、膨張させる
    alpha = img_vibrant.split()[3]
    # 二値化
    mask = alpha.point(lambda p: 255 if p > 50 else 0)
    
    # 膨張（外側に白い縁取りをつける）
    border_width = 10
    dilated_mask = mask.filter(ImageFilter.MaxFilter(border_width * 2 + 1))
    
    # 外枠の黒アウトライン用マスク（さらに少し膨張）
    outline_width = 4
    outer_mask = dilated_mask.filter(ImageFilter.MaxFilter(outline_width * 2 + 1))
    
    # 新しいキャンバス（余白を確保）
    w, h = img.size
    pad = 20
    canvas = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    
    # 外枠（黒いアニメ風アウトライン）
    black_layer = Image.new("RGBA", (w + pad * 2, h + pad * 2), (20, 25, 40, 255))
    canvas.paste(black_layer, (0, 0), ImageOps.expand(outer_mask, pad))
    
    # 白いステッカー縁（ポップなホワイトボーダー）
    white_layer = Image.new("RGBA", (w + pad * 2, h + pad * 2), (255, 255, 255, 255))
    canvas.paste(white_layer, (0, 0), ImageOps.expand(dilated_mask, pad))
    
    # ドロップシャドウ（ほんのり柔らかい影を最背面に）
    # 元の画像を中央にペースト
    canvas.paste(img_vibrant, (pad, pad), img_vibrant)
    
    # クロップ
    bbox = canvas.getbbox()
    if bbox:
        canvas = canvas.crop(bbox)
        
    os.makedirs(os.path.dirname(dst_path), exist_ok=True)
    canvas.save(dst_path, "PNG")
    print(f"Created Pop Sticker Badge: {dst_path} (size: {canvas.size})")

def make_pop_logo(src_path, dst_path):
    img = Image.open(src_path).convert("RGBA")
    
    # 彩度と明るさをブースト
    enhancer_col = ImageEnhance.Color(img)
    img_vibrant = enhancer_col.enhance(1.4)
    enhancer_con = ImageEnhance.Contrast(img_vibrant)
    img_vibrant = enhancer_con.enhance(1.2)
    enhancer_bri = ImageEnhance.Brightness(img_vibrant)
    img_vibrant = enhancer_bri.enhance(1.1)
    
    # 白いポップステッカー縁取りと黒アウトライン
    alpha = img_vibrant.split()[3]
    # ノイズ対策：しきい値を上げて微小ゴミを除去
    mask = alpha.point(lambda p: 255 if p > 80 else 0)
    # モルフォロジーオープニング（縮小 -> 膨張）で微細な孤立点を除去
    clean_mask = mask.filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))
    
    border_width = 9
    dilated_mask = clean_mask.filter(ImageFilter.MaxFilter(border_width * 2 + 1))
    outline_width = 4
    outer_mask = dilated_mask.filter(ImageFilter.MaxFilter(outline_width * 2 + 1))
    
    w, h = img.size
    pad = 24
    canvas = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    
    # 外側のコミック黒枠
    black_layer = Image.new("RGBA", (w + pad * 2, h + pad * 2), (15, 20, 35, 255))
    canvas.paste(black_layer, (0, 0), ImageOps.expand(outer_mask, pad))
    
    # 白いステッカー枠
    white_layer = Image.new("RGBA", (w + pad * 2, h + pad * 2), (255, 255, 255, 255))
    canvas.paste(white_layer, (0, 0), ImageOps.expand(dilated_mask, pad))
    
    # 本体
    canvas.paste(img_vibrant, (pad, pad), img_vibrant)
    
    bbox = canvas.getbbox()
    if bbox:
        canvas = canvas.crop(bbox)
        
    os.makedirs(os.path.dirname(dst_path), exist_ok=True)
    canvas.save(dst_path, "PNG")
    print(f"Created Pop Toon Logo: {dst_path} (size: {canvas.size})")

res_dir = r"c:\Users\kaito\UnityProjects\TurboCircuit\Assets\Resources\UI"
make_pop_logo(os.path.join(res_dir, "title_logo.png"), os.path.join(res_dir, "title_logo_pop.png"))
make_pop_sticker_badge(os.path.join(res_dir, "badge_track_0.png"), os.path.join(res_dir, "badge_track_0_pop.png"))
make_pop_sticker_badge(os.path.join(res_dir, "badge_track_1.png"), os.path.join(res_dir, "badge_track_1_pop.png"))
make_pop_sticker_badge(os.path.join(res_dir, "badge_track_2.png"), os.path.join(res_dir, "badge_track_2_pop.png"))
