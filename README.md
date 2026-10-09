# Quản lý hồ sơ sinh viên bằng cây AVL

Ứng dụng desktop (WPF, .NET 8) quản lý hồ sơ sinh viên. Dữ liệu lưu trong file Excel, còn mọi thao tác tìm, thêm, xóa, lọc theo khoảng đều chạy trên **cây nhị phân tìm kiếm tự cân bằng AVL** trong bộ nhớ.

## 1. Chạy ứng dụng

| Cách | Lệnh |
|---|---|
| Chạy khi đang phát triển | `dotnet run --project src/AVLStudentManagement.App` |
| Chạy test | `dotnet test src/AVLStudentManagement.Tests` |
| Đóng gói thành `.exe` gửi người khác | `dotnet publish src/AVLStudentManagement.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ../Publish` |

Gửi cho người khác: nén file `AVLStudentManagement.App.exe` cùng thư mục `data` (có `HoSoSinhVien.xlsx`), đặt `data` cạnh file `.exe`. Máy nhận không cần cài .NET hay Excel.

**Vị trí file dữ liệu:** `data\HoSoSinhVien.xlsx` trong thư mục project `AVLStudentManagement.App` khi chạy từ source, hoặc `data\` cạnh file `.exe` khi đã đóng gói.

## 2. Kiến trúc

```
src/
├─ AVLStudentManagement.Core/        thư viện logic, không phụ thuộc WPF
│  ├─ DataStructures/AvlTree.cs      cây AVL chứa Student, khóa là mã SV
│  ├─ Models/                        Student, Enums (Gender, Status, Grade), Catalog (khoa -> lớp)
│  ├─ Data/ExcelStudentRepository.cs đọc/ghi .xlsx bằng ClosedXML
│  ├─ Services/StudentService.cs     giữ 1 cây AVL + 2 HashSet, điều phối mọi thao tác, kiểm tra dữ liệu (Validate)
│  └─ Services/StudentException.cs   lớp lỗi duy nhất của chương trình (dữ liệu không hợp lệ, trùng khóa, file Excel sai)
├─ AVLStudentManagement.App/         giao diện WPF, MVVM (CommunityToolkit.Mvvm)
└─ AVLStudentManagement.Tests/       MSTest (123 test)
```

Luồng một thao tác ghi (thêm, sửa, xóa, nhập):

```
Giao diện -> ViewModel -> StudentService:
  1. Chuẩn hóa và kiểm tra hợp lệ (hàm `Validate` của StudentService)
  2. Kiểm tra trùng khóa (cây AVL + HashSet)
  3. Cập nhật cây AVL
  4. Lưu xuống Excel  (lỗi ghi file -> hoàn tác cây ở bước 3, báo lỗi)
```

Thao tác đọc (tìm theo mã SV, lọc) lấy thẳng từ cây, không đọc lại Excel; Top N sắp xếp lại danh sách theo điểm.

## 3. Cấu trúc dữ liệu: một cây AVL và hai tập băm

| Cấu trúc | Khóa | Dùng cho |
|---|---|---|
| `idTree`: `AvlTree` | `StudentId` (so theo giá trị số: 2 < 10 < 100) | Tìm, thêm, sửa, xóa, danh sách có thứ tự, vẽ cây |
| `nationalIds`: `HashSet<string>` | CCCD | Kiểm tra trùng CCCD, O(1) |
| `emails`: `HashSet<string>` | Email (không phân biệt hoa thường) | Kiểm tra trùng email, O(1) |

**Vì sao CCCD và email dùng `HashSet`, không dùng AVL:** hai trường này chỉ cần biết "đã có chưa", không cần sắp xếp hay tìm theo khoảng, nên băm O(1) là đủ.

Mỗi thao tác ghi cập nhật cây và cả hai tập băm.

## 4. Thuật toán AVL (`AvlTree.cs`)

Ký hiệu: n là số sinh viên, h là chiều cao cây (h ≤ 1,44·log₂(n+2)), k là số kết quả trả về.

### 4.1 Nền tảng: chiều cao và hệ số cân bằng
- Mỗi nút lưu sẵn `Height` (nút lá = 1, nút rỗng = 0). Chiều cao cây `Height` là O(1), không phải duyệt lại.
- `BalanceFactor = cao(trái) - cao(phải)`. Cây AVL luôn giữ hệ số này trong khoảng -1..1 tại **mọi** nút.

### 4.2 Cân bằng lại: `Rebalance`, `RotateLeft`, `RotateRight`
Sau mỗi lần thêm hoặc xóa, các nút trên đường đi từ chỗ thay đổi về gốc được cập nhật chiều cao và kiểm tra hệ số cân bằng. Nếu lệch quá 1 thì xoay:

| Ca | Điều kiện | Cách xử lý |
|---|---|---|
| Trái-Trái (LL) | lệch trái, con trái lệch trái hoặc cân bằng | xoay phải 1 lần |
| Phải-Phải (RR) | lệch phải, con phải lệch phải hoặc cân bằng | xoay trái 1 lần |
| Trái-Phải (LR) | lệch trái, con trái lệch phải | xoay trái con trái, rồi xoay phải |
| Phải-Trái (RL) | lệch phải, con phải lệch trái | xoay phải con phải, rồi xoay trái |

Mỗi phép xoay là O(1) và được đếm trong `RotationCount` (hiện ở thanh trạng thái: "Số lần xoay").

### 4.3 Các thao tác của cây

| Thao tác | Thuật toán | Độ phức tạp |
|---|---|---|
| `Insert` | Đệ quy xuống theo so sánh khóa, chèn lá, rồi `Rebalance` trên đường quay về. Khóa trùng thì trả về `false` | O(log n) |
| `Delete` | Tìm nút. 0 hoặc 1 con: lấy con lên thay. 2 con: lấy nút nhỏ nhất của cây con phải (nút kế tiếp) thay vào rồi xóa nút đó. Rồi `Rebalance` trên đường quay về | O(log n) |
| `Find` / `Update` | Đi từ gốc, nhỏ hơn thì sang trái, lớn hơn thì sang phải (vòng lặp). `Update` chỉ thay dữ liệu của nút, không đổi cấu trúc | O(log n) |
| `Min` / `Max` (chưa dùng trên giao diện) | Đi hết về bên trái / bên phải | O(log n) |
| `Range(from, to)` (chưa dùng trên giao diện) | Duyệt đệ quy, **cắt nhánh**: chỉ đi sang trái khi nút lớn hơn `from`, chỉ đi sang phải khi nút nhỏ hơn `to` | O(log n + k) |
| `InOrder` | Trái-gốc-phải (đệ quy), kết quả tăng dần | O(n) |
| `PreOrder` | Gốc-trái-phải (đệ quy) | O(n) |
| `PostOrder` | Trái-phải-gốc (đệ quy) | O(n) |

Các hàm đệ quy an toàn vì độ sâu chỉ bằng chiều cao cây h ≈ log n (khoảng 45 tầng cho 1 triệu sinh viên).

## 5. Chức năng và thuật toán AVL được dùng

Bảng dưới liệt kê đúng các chức năng đang có trên giao diện, và cây AVL tham gia vào từng chức năng như thế nào.

| Chức năng | Cây AVL làm gì | Độ phức tạp |
|---|---|---|
| **Mở app, nạp dữ liệu** | Đọc file Excel, với mỗi dòng: kiểm tra trùng mã SV bằng `Find`, CCCD và email bằng `HashSet`, rồi `Insert` vào cây (cây dựng theo thứ tự dòng trong file nên có nhiều lần xoay). Dựng vào cây tạm, đủ hợp lệ mới thay cây thật. Trùng khóa trong file thì báo lỗi kèm số dòng | O(n log n) |
| **Danh sách sinh viên** | `InOrder()`: bảng luôn theo mã SV tăng dần theo giá trị số (2 < 10 < 100), không cần sắp xếp lại. F5 hoặc "Bỏ lọc" quay về danh sách đầy đủ | O(n) |
| **Tìm theo mã SV** (ô "Mã SV") | `Find`: đi từ gốc, nhỏ hơn thì sang trái, lớn hơn thì sang phải, nên tối đa h bước. Thanh trạng thái báo "Tìm thấy sau N bước (cây có n sinh viên, cao h tầng)". Ở tab Cấu trúc cây, đường đi từ gốc xuống nút tìm được tô viền đỏ (cùng phép so sánh với `Find`). Không thấy thì báo số nút đã ghé qua | O(log n) |
| **Thêm sinh viên** (nút Thêm) | Chuẩn hóa, kiểm tra hợp lệ, kiểm tra trùng (`Find` + `HashSet`), rồi `Insert` (có thể xoay để cân bằng lại), lưu Excel. Lưu lỗi thì `Delete` lại để hoàn tác | O(log n) + ghi file |
| **Sửa sinh viên** (nút Lưu khi đang chọn một dòng) | `Find` bản cũ, kiểm tra hợp lệ và trùng, rồi `Update`: chỉ thay dữ liệu của nút. Mã SV không đổi được nên cây không đổi cấu trúc, không xoay. Lưu lỗi thì trả lại bản cũ | O(log n) + ghi file |
| **Xóa sinh viên** (nút Xóa, Delete, Ctrl+D; chọn nhiều dòng bằng Shift, Ctrl) | Với mỗi sinh viên đã chọn: `Find` rồi `Delete` (nút 0, 1 hoặc 2 con, rồi `Rebalance` trên đường quay về). Lưu file một lần cho cả nhóm. Lưu lỗi thì `Insert` lại tất cả | O(k log n) + ghi file |
| **Lọc theo lớp, trạng thái, xếp loại** | Không dùng tính chất của cây: duyệt `InOrder()` và bỏ qua sinh viên không khớp, nên kết quả vẫn theo mã SV | O(n) |
| **Top N** (ô "Top điểm cao") | Không dùng cây (cây chỉ sắp xếp theo mã SV): lấy `InOrder()` rồi sắp xếp theo điểm giảm dần và lấy N đầu | O(n log n) |
| **Sắp xếp theo cột** (bấm tiêu đề cột) | Dùng sắp xếp có sẵn của bảng, chỉ đổi cách hiện, không đụng cây | O(n log n) |
| **Nhập từ Excel** | Với mỗi sinh viên trong file: chuẩn hóa, kiểm tra hợp lệ (lớp phải có trong danh mục của file đang mở), kiểm tra trùng, `Insert`. Ghi Excel một lần, báo rõ từng sinh viên bị bỏ qua và lý do. Lưu lỗi thì hoàn tác toàn bộ | O(m log n) |
| **Xuất ra Excel** | Không dùng cây: chép file dữ liệu hiện tại (luôn mới nhất vì mỗi thao tác ghi đã lưu) | O(kích thước file) |
| **Tab Cấu trúc cây** (Ctrl+2) | Vẽ cây thật: x là thứ tự in-order, y là độ sâu; mỗi nút ghi mã SV, họ tên, `h` (chiều cao nút) và `bf` (hệ số cân bằng); nút lệch (\|bf\| = 1) tô cam. Rê chuột vào nút xem thông tin sinh viên. Xanh dương: sinh viên nằm trong kết quả tìm kiếm hoặc lọc đang hiện (các nút khác mờ đi). Viền đỏ: đường đi tìm kiếm | O(n) |
| **Số nút vẽ, phóng to/thu nhỏ** (tab Cấu trúc cây) | Ô "Số nút vẽ": duyệt theo từng tầng từ gốc (BFS) lấy N nút đầu, nên phần vẽ luôn nối liền với gốc; để trống = tất cả, tối đa 300 nút. `n`, `h`, `log₂(n)` trên thanh công cụ của tab tính theo phần đang vẽ. Nút − / + hoặc Ctrl + lăn chuột để phóng to, thu nhỏ (20% đến 300%) | O(N) |
| **Danh sách duyệt** (tab Cấu trúc cây) | Chọn Pre-order, In-order hoặc Post-order để xem dãy mã SV theo từng kiểu duyệt (cây có không quá 300 sinh viên) | O(n) |
| **Thanh trạng thái** | Tổng SV (`Count`), chiều cao h (`Height`, lưu sẵn trong nút nên O(1)), log₂(n), số lần xoay (`RotationCount`), thời gian thao tác gần nhất. Rê chuột vào từng ô để xem giải thích, ví dụ ô `h` hiện đường dài nhất từ gốc xuống lá, ô `log₂(n)` hiện phép tính | O(1) |
| **Kiểm tra trùng CCCD, email** | Không dùng cây: `HashSet` (CCCD, email không phân biệt hoa thường) | O(1) |

**Có trong code nhưng chưa có trên giao diện** (đã có test): `Range` (tìm theo khoảng mã SV, cắt nhánh, O(log n + k)), `Min`/`Max` của cây, và hai hàm của `StudentService` dùng `Range`: `FindByIdRange`, `DeleteByIdRange`.

## 6. Kiểm tra dữ liệu (hàm `Validate` của StudentService)

| Trường | Quy tắc |
|---|---|
| Mã SV | chỉ gồm chữ số, ít nhất 1 chữ số, không giới hạn độ dài; duy nhất, không sửa được sau khi tạo. Cây so sánh theo giá trị số (2 < 10), mã chỉ khác số 0 ở đầu (7 và 007) vẫn là hai mã khác nhau |
| Họ tên | 2-100 ký tự, tự bỏ khoảng trắng thừa |
| Ngày sinh | tuổi từ 15 đến 60 |
| Giới tính | Nam, Nữ hoặc Khác |
| CCCD | đúng 12 chữ số, duy nhất |
| Email | đúng định dạng, duy nhất (không phân biệt hoa thường) |
| Số điện thoại | tùy chọn; nếu có thì 10 số, bắt đầu bằng 0 |
| Địa chỉ | tối đa 255 ký tự |
| Khoa, Lớp | phải có trong danh mục, và lớp phải thuộc đúng khoa |
| Trạng thái | Đang học, Bảo lưu, Đã tốt nghiệp, Thôi học |
| Điểm TB | lớn hơn 0 (không giới hạn trên), làm tròn 2 chữ số |
| Xếp loại | không lưu, tính từ điểm: Xuất sắc ≥ 9,0; Giỏi ≥ 8,0; Khá ≥ 7,0; Trung bình ≥ 5,0; Yếu ≥ 4,0; Kém < 4,0 |

Lỗi hiện ngay cạnh ô nhập tương ứng.

## 7. Cơ sở dữ liệu Excel

File `HoSoSinhVien.xlsx` có hai sheet:
- `SinhVien`: dòng 1 là tiêu đề (đọc theo tên cột nên đổi thứ tự cột vẫn đúng), mỗi dòng sau là một sinh viên.
- `DanhMuc`: hai cột Khoa, Lop, dùng để đổ vào ô chọn và để kiểm tra.

**Ghi an toàn:** ghi ra file `.tmp`, rồi thay vào file chính và giữ một bản `.bak`. Nếu app tắt giữa chừng thì file chính không bị hỏng. Nếu file đang mở trong Excel làm ghi lỗi, thao tác được hoàn tác và app báo lỗi.

**Giới hạn:** mỗi thao tác ghi lại toàn bộ file (đo thực tế: 10.000 sinh viên ghi khoảng 1 giây). Phù hợp quy mô lớp, khoa. Nếu lên hàng chục nghìn thì nên chuyển sang SQLite.

## 8. Phím tắt

| Phím | Chức năng |
|---|---|
| Ctrl+N | Làm mới form để thêm mới |
| Ctrl+S | Lưu (thêm hoặc cập nhật) |
| Ctrl+D hoặc Delete | Xóa dòng đang chọn |
| F5 | Hiện tất cả |
| Ctrl+F hoặc F3 | Đến ô tìm nhanh |
| Ctrl+I / Ctrl+E | Nhập / Xuất Excel |
| Esc | Bỏ chọn, xóa form, đóng phần nâng cao |
| Ctrl+1 / Ctrl+2 | Tab Hồ sơ / Cấu trúc cây |
| Ctrl + lăn chuột (tab Cấu trúc cây) | Phóng to / thu nhỏ cây (hoặc dùng nút − / + trên thanh công cụ của tab) |
| Shift + bấm, Ctrl + bấm, Ctrl+A (bảng sinh viên) | Chọn nhiều dòng như Excel; bấm Xóa (hoặc Delete, Ctrl+D) để xóa tất cả các dòng đã chọn, chỉ ghi file một lần |
| F4 | Ẩn/hiện panel chi tiết |
| F1 | Bảng liệt kê phím tắt (cũng có ở menu Trợ giúp) |
| Ctrl+Q | Thoát |

## 9. Kiểm thử và hiệu năng

123 test, chia theo nhóm:
- **Cây AVL (27):** bốn ca xoay LL, RR, LR, RL; xóa lá, nút một con, nút hai con, gốc, cây một nút; `Range`, `Min`/`Max`, các kiểu duyệt; 10.000 thao tác ngẫu nhiên đối chiếu với `SortedSet`; chèn 100.000 khóa tăng dần mà chiều cao vẫn cỡ logarit.
- **Kiểm tra dữ liệu (45):** từng trường, gồm các giá trị sát biên.
- **Service (36):** trùng khóa, hoàn tác khi lưu lỗi, nhập hàng loạt, sửa/xóa khi chỉ có 1 sinh viên, Top N.
- **Excel (9) và file thực (1):** đọc/ghi, file hỏng, báo lỗi đúng dòng.
- **End-to-end (5):** thêm/sửa/xóa/nhập trên file Excel thật rồi mở lại; đo tốc độ.

Số đo trên 100.000 sinh viên (máy phát triển):

| Thao tác | Kết quả |
|---|---|
| Chiều cao cây | h = 20 (giới hạn AVL ≈ 25) |
| Tìm theo mã | ≈ 0,8 µs/lần |
| Tìm khoảng mã (50 kết quả) | ≈ 2 ms |
| Top 10 (phải sắp xếp lại cả danh sách) | ≈ 50 ms |
| Nạp và dựng cây | ≈ 640 ms |

Các số khoảng và Top 10 chỉ đo một lần nên gồm cả thời gian khởi động của .NET.

## 10. Dữ liệu mẫu

Thư mục `src/AVLStudentManagement.App/data` có hai file Excel, đều là dữ liệu sinh viên Trường Đại học Trà Vinh (TVU) do máy sinh ngẫu nhiên (họ tên, CCCD, số điện thoại chỉ để thử, không lấy từ Kaggle hay nguồn thật nào):

| File | Số sinh viên | Dùng để |
|---|---|---|
| `HoSoSinhVien.xlsx` | 100 | File app đọc khi chạy (demo). Đủ nhỏ để tab Cấu trúc cây vẽ được |
| `10000_sinh_vien.xlsx` | 10.000 | Thử hiệu năng. App **không** tự đọc file này; muốn dùng thì đổi tên thành `HoSoSinhVien.xlsx` |

- Mã SV đánh số 1, 2, 3, … (file demo: 1 đến 100; file 10.000: 101 đến 10.100, không trùng mã với file demo), email dạng `<tên><chữ cái đầu họ và tên đệm>@st.tvu.edu.vn` (ví dụ `Huỳnh Chí Bảo` thành `baohc@st.tvu.edu.vn`, trùng thì thêm số 2, 3, …). Trong file demo các dòng được xáo trộn thứ tự, vì app dựng cây theo thứ tự dòng, nên cây có đủ các ca xoay.
- 7 khoa/trường với các lớp như `DA25KTA` trong sheet `DanhMuc` (cả hai file đều có danh mục đủ 70 lớp, để nhập file 10.000 vào app đang mở file demo thì không sinh viên nào bị bỏ qua vì lớp lạ).
- Khoảng 60–65% sinh viên có địa chỉ ở Trà Vinh, số còn lại ở các tỉnh miền Tây. Điểm TB thang 10 phân bố quanh 7,2.

Mã, email và CCCD của file 10.000 đã được chỉnh để không trùng với file demo: dùng **Nhập Excel** chọn `10000_sinh_vien.xlsx` khi app đang mở file demo sẽ nhập đủ 10.000 sinh viên, app có 10.100 sinh viên (đo: khoảng 0,5 giây để nhập, rồi ghi file khoảng 1 giây).

Đo trên máy phát triển với file 10.000 sinh viên: nạp file và dựng cây khoảng 2 giây, mỗi lần thêm/sửa/xóa ghi lại file khoảng 1 giây, tìm theo mã dưới 1 micro giây, cây cao 14 tầng (log₂(10000) ≈ 13,3). Tab Cấu trúc cây vẽ tối đa 300 nút (cây lớn hơn thì vẽ 300 nút đầu tiên theo từng tầng từ gốc).
