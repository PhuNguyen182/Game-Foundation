### Hướng dẫn sử dụng Addressable Group Definition Files (AGDF)

Tính năng này giúp bạn tạo và đồng bộ các Addressable Group dựa trên các ScriptableObject gọi là Addressable Group Definition File (AGDF). Mỗi AGDF đại diện cho một thư mục nội dung và tự động tạo/cập nhật nhóm Addressables theo mẫu (template) cục bộ hoặc từ xa.

---

## Yêu cầu
- Unity Addressables (đã cấu hình trong dự án).
- Sirenix Odin Inspector (các thuộc tính như InfoBox, Button, v.v.).
- Định nghĩa symbol `USE_EXTENDED_ADDRESSABLE` trong Scripting Define Symbols để bật mã trong thư mục này.

---

## Khái niệm chính
- **AGDF (AddressableGroupDefinitionFile)**: ScriptableObject cốt lõi nắm thông tin nhóm, danh sách entry, tên nhóm, và thao tác đồng bộ.
- **Template (`AddressableGroupDefinitionFileTemplate`)**: Chứa 2 mẫu nhóm Addressables (Local/Remote) cùng các ràng buộc build (tùy chọn). AGDF sẽ tự tìm và gán template đầu tiên có trong dự án nếu chưa được gán.
- **BuildAndLoadMode**: Phân loại nhóm theo chế độ Local hoặc Remote.
- **Chiến lược (Strategy AGDF)**: Các lớp dẫn xuất AGDF theo kịch bản Local/Remote và kiểu gom gói (Chunked/Individually). Mỗi lớp có Editor tùy biến để tự động tạo/cập nhật nhóm.

---

## Các tệp quan trọng
- `AddressableGroupDefinitionFile` (trừu tượng): Cung cấp trường dữ liệu, nút thao tác và logic cập nhật/đồng bộ.
- `AddressableGroupDefinitionFileTemplate`: ScriptableObject khai báo template nhóm Local/Remote (kiểu `AddressableAssetGroupTemplate`).
- Các AGDF cụ thể:
  - `LocalChunkedStrategyAGDF`
  - `LocalIndividuallyStrategyAGDF`
  - `RemoteChunkedStrategyAGDF`
  - `RemoteIndividuallyStrategyAGDF`
- Editors tương ứng cho mỗi chiến lược: đảm nhiệm tạo nhóm từ template, gán thư mục hiện tại làm entry, và đồng bộ danh sách entry hiển thị trong AGDF.

---

## Trường dữ liệu chính (trong AGDF)
- `GroupName` (chỉ đọc, suy ra): Lấy từ `overrideName` nếu `useOverrideName` bật, ngược lại dùng `groupName`.
- `BuildAndLoadMode`: Local hoặc Remote (tùy lớp con).
- `template`: Tham chiếu đến `AddressableGroupDefinitionFileTemplate`. Tự động gán nếu bỏ trống thông qua `SetupTemplate()`.
- `folderName` (readonly): Đường dẫn thư mục chứa AGDF.
- `cachedGroupName` (readonly): Tên nhóm đã lưu để phục vụ đổi tên/xóa nhóm cũ khi cần.
- `groupName` (readonly): Tên nhóm hiện tại.
- `useOverrideName` + `overrideName`: Cho phép đặt tên nhóm tùy chỉnh khác với mặc định.
- `assetEntries`: Danh sách các entry (địa chỉ, đường dẫn, nhãn) được đồng bộ từ nhóm.
- `buildVersionConstraints`, `buildPlatformConstraints`: Ràng buộc build tùy chọn (copy từ template nếu có).

Mỗi `AssetEntryInfo` gồm:
- `address` (readonly)
- `assetPath` (readonly)
- `labels`: Danh sách nhãn Addressables. Có nút `Locate` để ping tài sản.

---

## Các nút thao tác trên AGDF
- `UpdateGroup()`
  - Đồng bộ địa chỉ và nhãn của các entry trong nhóm cho khớp `assetEntries`.
  - Nếu `useOverrideName` bật: đổi tên nhóm sang `overrideName`, đánh dấu thay đổi; sau đó xóa nhóm cũ dựa trên `cachedGroupName` nếu có.
  - Lưu và đánh dấu thay đổi tài sản.
- `DeleteGroup()`
  - Xóa nhóm Addressables tương ứng với `GroupName`.
  - Xóa luôn asset AGDF khỏi dự án.
- `SimplifyAddresses()`
  - Đặt address của từng entry bằng tên file (không phần mở rộng) cho toàn bộ entry trong cùng nhóm (trừ thư mục và chính AGDF).
- `LocateGroup()`
  - Ping đối tượng nhóm trong cửa sổ Addressables Groups.

---

## Editors cho từng chiến lược
Mỗi lớp Editor thực hiện khi mở/awake:
1. Xác định đường dẫn asset AGDF và thư mục chứa.
2. Gọi `SetupTemplate()` để bảo đảm có template.
3. Xác định `groupName`:
   - Nếu AGDF chưa có tên, lấy từ tên file ScriptableObject.
   - Nếu có `overrideName`, dùng tên đó.
4. Tạo nhóm nếu chưa có bằng cách sao chép schemas từ template tương ứng (Local/Remote) và gọi `CreateGroup`.
5. Nếu nhóm tồn tại và tên khác mong muốn, cập nhật lại tên nhóm.
6. Gán `groupName` và `cachedGroupName` cho AGDF.
7. Đồng bộ entries:
   - Với kiểu Chunked: thêm entry là chính thư mục chứa AGDF vào nhóm và gán address theo tên thư mục. Sau đó đọc toàn bộ entry trong nhóm để cập nhật `assetEntries`.
   - Với kiểu Individually: quét tất cả asset trong thư mục (trừ thư mục con/AGDF), `CreateOrMoveEntry` vào nhóm từng cái. Sau đó đọc lại danh sách `group.entries` để cập nhật `assetEntries`.

Các file Editor:
- `LocalChunkedStrategyAGDFEditor`
- `LocalIndividuallyStrategyAGDFEditor`
- `RemoteChunkedStrategyAGDFEditor`
- `RemoteIndividuallyStrategyAGDFEditor`

Lưu ý: Các Editor đều sử dụng `AddressableAssetSettingsDefaultObject.Settings`. Hãy đảm bảo Addressables đã được khởi tạo trong dự án.

---

## Quy trình sử dụng đề xuất
1. Bật define symbol `USE_EXTENDED_ADDRESSABLE` trong Project Settings → Player → Scripting Define Symbols.
2. Tạo một `AddressableGroupDefinitionFileTemplate` tại menu:
   - Create → ExtendedAddressable → Group Definition → AddressableGroupDefinitionFileTemplate
   - Gán `localTemplateGroup` và `remoteTemplateGroup` bằng các `AddressableAssetGroupTemplate` sẵn có.
3. Chọn thư mục nội dung bạn muốn đưa vào Addressables. Trong thư mục đó, tạo một AGDF phù hợp:
   - Local: `LocalChunkedStrategyAGDF` hoặc `LocalIndividuallyStrategyAGDF`
   - Remote: `RemoteChunkedStrategyAGDF` hoặc `RemoteIndividuallyStrategyAGDF`
4. Mở asset AGDF vừa tạo để kích hoạt Editor tương ứng. Nhóm Addressables sẽ tự được tạo/cập nhật:
   - Chunked: thêm entry là thư mục, gom đóng gói theo thư mục.
   - Individually: thêm từng asset riêng lẻ trong thư mục vào nhóm.
5. Tùy chọn: bật `useOverrideName` và nhập `overrideName` để đổi tên nhóm theo ý muốn.
6. Dùng các nút:
   - `UpdateGroup`: đồng bộ địa chỉ, nhãn, và xử lý đổi tên/xóa nhóm cũ khi dùng override.
   - `SimplifyAddresses`: chuẩn hóa địa chỉ bằng tên file.
   - `LocateGroup`: nhanh chóng tìm nhóm trên Addressables Groups.
   - `DeleteGroup`: xóa nhóm và xóa luôn AGDF khi không còn cần.

---

## Ghi chú và cảnh báo
- Có TODO trong mã: “nếu tôi cập nhật remote hoặc local tới nhóm đích, sẽ crash nếu xóa file AGDF”. Tránh thao tác xóa AGDF ngay sau khi đổi Local/Remote hoặc khi nhóm đang được tham chiếu; nên đóng/mở lại Editor hoặc bảo đảm không còn tham chiếu trước khi xóa.
- `SetupTemplate()` sẽ chọn template đầu tiên tìm thấy trong dự án nếu `template` trống. Nếu bạn có nhiều template, hãy gán thủ công để đúng mẫu mong muốn.
- Khi bật `useOverrideName`, quá trình `UpdateGroup()` sẽ cố gắng xóa nhóm cũ dựa trên `cachedGroupName`. Đảm bảo tên cache phản ánh chính xác trạng thái trước đó.
- `SimplifyAddresses()` chỉ áp dụng cho các entry trong nhóm và bỏ qua thư mục hoặc asset không hợp lệ.
- Phụ thuộc Odin Inspector: nếu không có Odin, các thuộc tính hiển thị (InfoBox, Button, HorizontalGroup, ReadOnly, ShowIf, v.v.) sẽ không hoạt động như trong hình dung UI.

---

## Mẹo
- Duy trì một template chuẩn cho Local và Remote để đảm bảo nhóm mới tạo có cấu hình schemas nhất quán (ví dụ: `BundledAssetGroupSchema`).
- Dùng chiến lược Individually khi bạn muốn kiểm soát chi tiết từng asset; dùng chiến lược Chunked khi muốn gom theo thư mục để đơn giản địa chỉ và đóng gói.
- Kết hợp `SimplifyAddresses()` sau khi thêm asset để địa chỉ dễ nhớ, đồng nhất với tên file.

---

## Khắc phục sự cố
- Không thấy nhóm tạo ra: kiểm tra Addressables đã khởi tạo và define symbol `USE_EXTENDED_ADDRESSABLE` đã bật.
- Template null: tạo `AddressableGroupDefinitionFileTemplate` và gán vào AGDF hoặc để `SetupTemplate()` tự tìm. Hãy đảm bảo trong dự án có ít nhất một template.
- Đổi tên nhóm không phản ánh: chạy `UpdateGroup()` sau khi bật `useOverrideName` và nhập `overrideName`.
- Crash khi xóa AGDF sau khi chuyển chế độ Local/Remote: tạm thời tránh quy trình đó; xóa nhóm từ cửa sổ Addressables trước, hoặc khởi động lại Editor rồi xóa AGDF.

---

## Menu tạo asset
- Create → ExtendedAddressable → Group Definition → Local AGDF → LocalChunkedStrategyAGDF
- Create → ExtendedAddressable → Group Definition → Local AGDF → LocalIndividuallyStrategyAGDF
- Create → ExtendedAddressable → Group Definition → Remote AGDF → RemoteChunkedStrategyAGDF
- Create → ExtendedAddressable → Group Definition → Remote AGDF → RemoteIndividuallyStrategyAGDF
- Create → ExtendedAddressable → Group Definition → AddressableGroupDefinitionFileTemplate

---

## Bản quyền và đóng góp
Mã nguồn thuộc module PracticalModules. Bạn có thể mở rộng các chiến lược bằng cách kế thừa `AddressableGroupDefinitionFile` và viết Editor tương ứng để tùy biến quy tắc gom nhóm, đặt địa chỉ, nhãn, và schemas.
