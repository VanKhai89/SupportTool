# Memory Viewer - UI Sketch (Structure Dissect Edition)

Bản phác thảo giao diện được thiết kế đặc biệt cho mục đích dò tìm, ghi chú và giải phẫu cấu trúc biến (Struct/Class) trong game.

## 1. Giao diện chính (Main Window)

```text
+---------------------------------------------------------------------------------------------------------+
| Memory Structure Viewer v1.0                                                                      [-][X]|
+---------------------------------------------------------------------------------------------------------+
| [ Select Process... ]  Status: Attached to [Game.exe - PID: 1234]                                       |
+---------------------------------------------------------------------------------------------------------+
|                                                                                                         |
| Base Address: [ 0x00401000 ] [ Add Address / Pointer ]                                                  |
|                                                                                                         |
| Delta Range:  From [ -100                ]   To [ 200                     ]                             |
|                                                                                                         |
| Range Filter: From [                     ]   To [                         ]  [ Apply Filter ] [ Clear ] |
|                                                                                                         |
| [ Take Snapshot (Apply Base) ] <--- Bấm để lưu các giá trị hiện tại làm mốc so sánh                     |
|                                                                                                         |
+---------------------------------------------------------------------------------------------------------+
| Offset | Address    | Hex         | Int32         | Float             | Label / Ghi chú | Freeze & Value|
+--------+------------+-------------+---------------+-------------------+-----------------+---------------+
| +0x00  | 0x00401000 | 64 00 00 00 | 100           | 1.4E-43           | [ Vàng (Base) ] | [ ]           |
| +0x04  | 0x00401004 | 50 00 00 00 | 80 (-20)      | 1.1E-43 (-3.0E)   | [ Mới mua đồ  ] | [x] [ 50    ] |
| +0x08  | 0x00401008 | 96 00 00 00 | 150 (+50)     | 2.1E-43 (+0.7E)   | [ Vừa lụm 50  ] | [ ]           |
| +0x0C  | 0x0040100C | 00 00 48 43 | 11287..       | 190.0 (-10.0)     | [ Tọa độ Y    ] | [ ]           |
| ...    | ...        | ...         | ...           | ...               | [             ] | ...           |
+---------------------------------------------------------------------------------------------------------+
| Status: Reading 75 entries... | Snapshot: Active | Lớn hơn: Xanh (+Delta) - Nhỏ hơn: Đỏ (-Delta)        |
+---------------------------------------------------------------------------------------------------------+
```

### Chú thích Main Window:
- **Nút [ Take Snapshot (Apply Base) ]**: Khi bấm, hệ thống sẽ lưu lại toàn bộ mảng dữ liệu hiện hành làm "Giá trị gốc". Bạn có thể bấm lại bất kỳ lúc nào để reset mốc so sánh.
- **Range Filter**: Bộ lọc giá trị (Từ - Đến). Chỉ giữ lại những dòng có chứa giá trị (Int32 hoặc Float) nằm trong khoảng được chỉ định, giúp loại bỏ nhanh các địa chỉ rác.
- **DataGrid hiển thị đa kiểu (Multi-Type View)**: Hiển thị đồng thời Hex, Int32, Float. Giúp người dùng quét mắt ngang 1 dòng là đoán ngay dữ liệu này nên được đọc dưới dạng gì.
- **Màu sắc & Chênh lệch (Inline Delta)**: Khi giá trị Real-time khác với "Giá trị gốc" (Snapshot):
  - Hiển thị trực tiếp mức chênh lệch trong ngoặc đơn: `CurrentValue (±Delta)` áp dụng cho Int32 và Float.
  - Nếu **Lớn hơn**: Ô đó tự động tô màu nền **XANH LÁ** và hiển thị dạng `150 (+50)`.
  - Nếu **Nhỏ hơn**: Ô đó tự động tô màu nền **ĐỎ** và hiển thị dạng `80 (-20)`.
  - Đứng im (Bằng gốc): Màu bình thường, chỉ hiển thị giá trị hiện hành.
- **Cột Offset**: Tọa độ tương đối so với Base Address, tối quan trọng để copy cấu trúc code sau này (VD: `Entity -> +0x0C = Tọa độ Y`).
- **Cột Label**: TextBox cho phép gõ tự do để lưu lại các dự đoán trong lúc dò tìm.

---

## 2. Cửa sổ Thêm Địa chỉ (Add Address Dialog)

Giữ nguyên mô hình Cheat Engine chuẩn để tính toán Pointer trực quan:

```text
+-------------------------------------------------------------+
| Add address                                           [-][X]|
+-------------------------------------------------------------+
| Address:                                                    |
| [ 0x00401000                               ] = 12345        |
|                                                             |
| Description:                                                |
| [ Player Base Object                       ]                |
|                                                             |
| Type:                                                       |
| [ 4 Bytes (Int32) v]                                        |
|                                                             |
| [x] Pointer                                                 |
| +---------------------------------------------------------+ |
| | < [ 0x14      ] >  0x00401000 + 0x14 = 0x00401014       | |
| |                  -> 56789                               | |
| |                                                         | |
| | < [ 0x1A4     ] >  0x004A0000 + 0x1A4 = 0x004A01A4      | |
| |                  -> 12345                               | |
| |                                                         | |
| |     [ Add Offset ]        [ Remove Offset ]             | |
| +---------------------------------------------------------+ |
|                                                             |
|               [ OK ]          [ Cancel ]                    |
+-------------------------------------------------------------+
```
