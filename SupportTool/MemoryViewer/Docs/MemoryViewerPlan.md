# Memory Viewer Tool - Phân tích yêu cầu và Kế hoạch thực hiện

## 1. Phân tích yêu cầu

### Mục tiêu
Xây dựng một công cụ hỗ trợ xem và phân tích cấu trúc bộ nhớ (Memory Viewer / Structure Dissect) phục vụ cho việc mò mẫm, dự đoán và ghi chú các biến trong game dựa trên một địa chỉ Base (ví dụ tìm ra Vàng, sau đó quét xung quanh để tìm Gỗ, Máu, Tọa độ).

### Các tính năng chính
1. **Quản lý Process**:
   - Liệt kê và chọn process đang chạy để attach.
2. **Giao diện nhập địa chỉ (Add Address Dialog)**:
   - Cửa sổ riêng biệt mô phỏng Cheat Engine để nhập địa chỉ gốc hoặc Pointer Chain (Con trỏ đa cấp). Tính toán và hiển thị giá trị ngay lập tức (Real-time Preview).
3. **Hiển thị vùng nhớ Đa định dạng (Multi-Type Structure View)**:
   - Thay vì chỉ xem 1 kiểu, hiển thị đồng thời nhiều kiểu dữ liệu trên cùng 1 hàng (**Hex | Int32 | Float**). Giúp người dùng nhìn phát đoán được ngay kiểu biến của ô nhớ đó mà không cần đổi bộ lọc.
   - Hiển thị thêm cột **Offset** (ví dụ: `+0x00`, `+0x04`) để dễ dàng ghi nhận cấu trúc tương đối so với Base Address.
4. **Hệ thống Snapshot & Highlight thay đổi (So sánh giá trị)**:
   - Nút **[ Take Snapshot / Apply ]**: Lưu lại toàn bộ giá trị hiện hành trên lưới làm "Giá trị gốc" (Baseline).
   - **Tự động Highlight & Hiển thị chênh lệch (Inline Delta)**: Liên tục so sánh giá trị Real-time với "Giá trị gốc". 
     - Hiển thị trực tiếp mức chênh lệch bên cạnh giá trị hiện tại theo định dạng: `Giá trị hiện tại (±Lượng thay đổi)` (chỉ áp dụng cho Int32 và Float).
     - Nếu lớn hơn -> Hiển thị `Value (+Delta)` và tô nền màu Xanh (Green). 
     - Nếu nhỏ hơn -> Hiển thị `Value (-Delta)` và tô nền màu Đỏ (Red). 
     - Nếu bằng -> Hiển thị `Value` bình thường (Màu trắng). 
     - Chức năng này cho phép lọc bằng mắt cực nhanh khi quay lại game làm 1 hành động (VD: mua đồ tốn 50 vàng -> liếc tìm ô có đuôi `(-50)`).
5. **Đóng băng (Freeze) và Ghi chú (Label)**:
   - Có cột để người dùng gõ ghi chú tự do (VD: "Đoán là Máu").
   - Cột Checkbox "Freeze" để liên tục khóa (ghi đè) giá trị xuống ô nhớ bằng một Background worker.
6. **Lọc giá trị (Range Filter)**: Lọc các địa chỉ rác thông qua giá trị (Từ giá trị - Đến giá trị).

---

## 2. Đề xuất công nghệ
- **Ngôn ngữ / Framework**: C# (WPF). Rất mạnh trong việc Binding UI, hỗ trợ DataGrid đổi màu ô tự động (Highlight) dựa trên so sánh giá trị dễ dàng bằng Converter/DataTriggers.
- **Tương tác bộ nhớ**: Sử dụng P/Invoke (`kernel32.dll` -> `OpenProcess`, `ReadProcessMemory`, `WriteProcessMemory`).

---

## 3. Các giai đoạn thực hiện (Phases)

### Phase 1: Core API & Quản lý Process
- Khai báo API, viết module `MemoryReader`.
- UI chọn process và thực hiện lệnh Attach.

### Phase 2: Add Address Dialog & Cấu trúc Pointer
- Hoàn thiện UI nhập Address có tích hợp Pointer Chain. Tự động cộng trừ tính toán Pointer ra địa chỉ thật để lấy Base Address.

### Phase 3: Structure View & Grid Hiển thị đa kiểu
- Xây dựng DataGrid hiển thị các cột: Offset, Address, Hex, Int32, Float, Label.
- Tính toán vòng lặp đọc bộ nhớ hàng loạt xung quanh Base Address với biên độ Delta (VD: -100 đến +200).

### Phase 4: Snapshot, Highlight và Đóng Băng (Freeze)
- Tích hợp nút Snapshot (chụp mảng giá trị lưu vào memory tạm).
- Thuật toán so sánh: Render đổi màu Xanh/Đỏ/Trắng trên GridView theo thời gian thực (update rate khoảng 100ms - 200ms).
- Thêm chức năng Freeze và chỉnh sửa giá trị trực tiếp/lưu ghi chú.
