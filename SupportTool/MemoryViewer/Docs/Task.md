# Memory Viewer Tasks

## Phase 1: Core API & Quản lý Process
- `[x]` Tạo Project WPF `MemoryViewer`.
- `[x]` Khởi tạo các thư mục dự án (Sources/Core, Models, Utils, ViewModels, Views).
- `[x]` Viết `Sources/Core/NativeMethods.cs` (P/Invoke kernel32.dll).
- `[x]` Viết `Sources/Core/ProcessManager.cs` (Quản lý Process).
- `[x]` Viết `Sources/Core/MemoryReader.cs` (Đọc/Ghi mảng byte, giải mã pointer).

## Phase 2: Add Address Dialog & Cấu trúc Pointer
- `[x]` Viết `Models/PointerOffsetItem.cs`.
- `[x]` Viết `Utils/HexConverter.cs`.
- `[x]` Xây dựng `ViewModels/AddAddressViewModel.cs`.
- `[x]` Xây dựng `Views/AddAddressWindow.xaml`.

## Phase 3: Structure View & Grid Hiển thị đa kiểu
- `[x]` Viết `Models/MemoryItem.cs` (Data Model).
- `[x]` Viết `Utils/DeltaScanner.cs`.
- `[x]` Xây dựng `Views/MainForm.cs` với DataGridView.
- `[x]` Xây dựng `Views/AddAddressForm.cs` với Type Combobox.

## Phase 4: Snapshot, Highlight và Đóng Băng (Freeze)
- `[x]` Tích hợp logic xử lý logic vào `MainForm.cs`.
- `[x]` Thêm event CellFormatting đổi màu Xanh/Đỏ dựa trên Snapshot.
- `[x]` Khởi tạo Timer để refresh UI và Freeze giá trị.
