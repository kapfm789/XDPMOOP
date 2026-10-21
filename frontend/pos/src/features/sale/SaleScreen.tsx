import {
  ApiError,
  errorMessage,
  type PaymentMethod,
  type PosSku,
  sendRemoteScan,
  type StockShortage,
  useOnline,
  useRemoteScan,
} from '@oism/shared';
import { Alert, Badge, Card, Col, Flex, Grid, type InputRef, message, Modal, Row, Tabs, theme } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { usePosBranch } from '../branch/usePosBranch';
import { addSku, applyShortages, type CartLine, cartCount, cartTotal, exactMatch, removeLine, setQuantity, toOrderLines } from '../cart/cart';
import { CartPanel } from '../cart/CartPanel';
import { PaymentPanel } from '../checkout/PaymentPanel';
import { useCheckout } from '../checkout/useCheckout';
import { PaymentSlip } from '../receipt/PaymentSlip';
import { ScanModal } from '../scan/ScanModal';
import { SearchPanel } from '../search/SearchPanel';
import { usePosSearch } from '../search/usePosSearch';

const REMOTE_SCAN_KEY = 'oism.pos.sendScansToPc';

// Màn hình bán hàng: tìm hoặc quét, giỏ, thanh toán trên một màn hình. Bố cục và phím tắt: docs/design/ui/pos.md.
export function SaleScreen() {
  const navigate = useNavigate();
  const wide = Grid.useBreakpoint().md;
  const { token } = theme.useToken();
  const [toast, toastHolder] = message.useMessage();
  const [modal, modalHolder] = Modal.useModal();

  const branch = usePosBranch();
  const online = useOnline();
  const [text, setText] = useState('');
  const [cart, setCart] = useState<CartLine[]>([]);
  const [selected, setSelected] = useState<string>();
  const [method, setMethod] = useState<PaymentMethod>('Cash');
  const [scanning, setScanning] = useState(false);
  // W5-15: điện thoại làm máy quét cho máy tính; lựa chọn được nhớ trên trình duyệt này.
  const [sendToPc, setSendToPc] = useState(() => localStorage.getItem(REMOTE_SCAN_KEY) === 'true');
  const search = usePosSearch(branch.branchId, text);
  const { checkout, pending } = useCheckout();

  // Hai lần quét liên tiếp có thể cùng đang chờ kết quả tìm: lần về sau phải cộng vào giỏ đã có lần về trước.
  const latestCart = useRef(cart);
  function updateCart(next: CartLine[]) {
    latestCart.current = next;
    setCart(next);
  }

  // Giỏ thuộc về một chi nhánh: đổi chi nhánh ở thanh trên thì bắt đầu giỏ mới.
  const cartBranch = useRef(branch.branchId);
  useEffect(() => {
    if (cartBranch.current === branch.branchId) return;
    cartBranch.current = branch.branchId;
    updateCart([]);
  }, [branch.branchId]);

  const searchInput = useRef<InputRef>(null);
  // Trên thiết bị cầm tay ô tìm không tự giành lại focus sau mỗi lần chạm, để bàn phím ảo không bật lên liên tục.
  // Hộp quét đang mở thì focus ở lại trong hộp; đóng hộp xong ô tìm mới lấy lại.
  const focusSearch = () => {
    if (wide && !scanning) searchInput.current?.focus();
  };

  // typed: chữ đã quét ra SKU này; ô tìm chỉ được xóa nếu nó vẫn còn đúng chữ đó. Trả về false khi chạm tồn khả dụng.
  function add(sku: PosSku, typed?: string): boolean {
    const next = addSku(latestCart.current, sku);
    const added = next !== latestCart.current;
    if (!added) toast.warning(sku.available > 0 ? `${sku.name}: chỉ còn ${sku.available}` : `${sku.name} đã hết hàng`);
    updateCart(next);
    setSelected(sku.skuId);
    setText((current) => (typed === undefined || current === typed ? '' : current));
    focusSearch();
    return added;
  }

  function changeQuantity(skuId: string, quantity: number) {
    const line = latestCart.current.find((item) => item.skuId === skuId);
    if (line && quantity > line.available) toast.warning(`${line.name}: chỉ còn ${line.available}`);
    updateCart(setQuantity(latestCart.current, skuId, quantity));
    setSelected(skuId);
    focusSearch();
  }

  function remove(skuId: string) {
    updateCart(removeLine(latestCart.current, skuId));
    focusSearch();
  }

  // Enter khi ô tìm có chữ: khớp đúng một mã vạch hoặc mã SKU thì thêm vào giỏ; không thì kết quả tìm đã hiện sẵn.
  // announce: mã đến từ hộp quét, hộp đang che giỏ và kết quả nên báo luôn đã thêm hay không khớp.
  async function addScanned(typed: string, announce = false) {
    try {
      const match = exactMatch(await search.searchNow(typed), typed);
      if (match) {
        if (add(match, typed) && announce) toast.success(`Đã thêm ${match.name}`);
      } else if (announce) {
        toast.warning(`Không có sản phẩm khớp mã ${typed}`);
      }
    } catch (error) {
      toast.error(errorMessage(error));
    }
  }

  // Mã đọc từ camera hoặc ảnh đi đúng đường của máy quét: vào ô tìm rồi như Enter.
  function addFromCamera(code: string) {
    setText(code);
    void addScanned(code, true);
  }

  // UC-POS-01 AC-9: mã do điện thoại cùng tài khoản gửi tới được xử lý như mã đọc từ hộp quét.
  useRemoteScan(addFromCamera);

  // Điện thoại đang làm máy quét: mã không vào giỏ ở đây mà được gửi sang máy tính; chỉ báo đã gửi.
  function onScanCode(code: string) {
    if (!sendToPc) return addFromCamera(code);
    sendRemoteScan(code).then(
      () => {
        navigator.vibrate?.(60);
        toast.success(`Đã gửi mã ${code}`);
      },
      (error: unknown) =>
        toast.error(error instanceof Error && error.message === 'hub_disconnected' ? 'Chưa kết nối máy chủ, mã chưa được gửi' : 'Không gửi được mã'),
    );
  }

  function changeSendToPc(next: boolean) {
    setSendToPc(next);
    localStorage.setItem(REMOTE_SCAN_KEY, String(next));
  }

  async function pay() {
    const lines = latestCart.current;
    // Mất mạng thì khóa thanh toán, kể cả khi bấm Enter (docs/design/ui/pos.md mục "PWA").
    if (!branch.branchId || lines.length === 0 || !online) return;
    try {
      const order = await checkout({
        branchId: branch.branchId,
        items: toOrderLines(lines),
        payment: { method, amount: cartTotal(lines) },
      });
      if (!order) return;
      void search.refresh();
      // Chỉ in sau khi backend trả đơn đã lưu.
      navigate(`/receipt/${order.id}`, { state: { order } });
    } catch (error) {
      if (error instanceof ApiError && error.code === 'insufficient_stock' && Array.isArray(error.details)) {
        // Tồn đã đổi từ lúc thêm vào giỏ: giỏ nhận số còn lại, thu ngân quyết định tiếp.
        const shortages = error.details as StockShortage[];
        const names = shortages.flatMap((shortage) => {
          const line = lines.find((item) => item.skuId === shortage.skuId);
          return line ? [`${line.name} (còn ${shortage.available})`] : [];
        });
        toast.warning(`Không đủ hàng: ${names.join(', ')}`);
        updateCart(applyShortages(latestCart.current, shortages));
        void search.refresh();
      } else {
        toast.error(errorMessage(error));
      }
      focusSearch();
    }
  }

  // Phiếu tạm tính kèm mã VietQR cho khách quét: in từ giỏ, không gọi API, không tạo đơn (UC-POS-03 AC-4).
  function printSlip() {
    window.print();
    focusSearch();
  }

  function clearCart() {
    if (latestCart.current.length === 0) return;
    modal.confirm({
      title: 'Xóa toàn bộ giỏ hàng?',
      okText: 'Xóa giỏ',
      cancelText: 'Giữ lại',
      onOk: () => updateCart([]),
      afterClose: focusSearch,
    });
  }

  // Phím tắt của màn hình. Handler đọc state mới nhất qua ref, nên chỉ đăng ký một lần.
  const onKey = useRef<(event: KeyboardEvent) => void>(() => {});
  onKey.current = (event) => {
    if (event.key === 'F2') {
      event.preventDefault();
      searchInput.current?.focus();
    } else if (event.key === 'F4') {
      event.preventDefault();
      setMethod(method === 'Cash' ? 'QR' : 'Cash');
    }

    // Các phím còn lại chỉ có nghĩa khi con trỏ đang ở ô tìm; hộp thoại và ô khác tự xử lý phím của chúng.
    if (document.activeElement !== searchInput.current?.input) return;
    const line = cart.find((item) => item.skuId === selected);
    if (event.key === 'Enter') {
      event.preventDefault();
      void (text.trim() ? addScanned(text) : pay());
    } else if (event.key === 'Escape') {
      if (text) setText('');
      else clearCart();
    } else if (!text && line && (event.key === '+' || event.key === '-')) {
      event.preventDefault();
      changeQuantity(line.skuId, line.quantity + (event.key === '+' ? 1 : -1));
    } else if (!text && line && event.key === 'Delete') {
      event.preventDefault();
      remove(line.skuId);
    }
  };
  useEffect(() => {
    const listener = (event: KeyboardEvent) => onKey.current(event);
    window.addEventListener('keydown', listener);
    return () => window.removeEventListener('keydown', listener);
  }, []);

  const searchPanel = (
    <SearchPanel
      inputRef={searchInput}
      autoFocus={!!wide}
      text={text}
      onTextChange={setText}
      skus={search.skus}
      loading={search.loading}
      inCart={(skuId) => cart.find((item) => item.skuId === skuId)?.quantity ?? 0}
      onPick={(sku) => add(sku)}
      onScan={() => setScanning(true)}
    />
  );
  const cartPanel = <CartPanel cart={cart} selected={selected} onSelect={setSelected} onQuantity={changeQuantity} onRemove={remove} />;
  const paymentPanel = (
    <PaymentPanel
      method={method}
      onMethodChange={(next) => {
        setMethod(next);
        focusSearch();
      }}
      total={cartTotal(cart)}
      bankBin={branch.current?.bankBin}
      bankAccount={branch.current?.bankAccount}
      onPrintSlip={printSlip}
      disabled={!branch.branchId || cart.length === 0 || !online}
      pending={pending}
      showShortcuts={!!wide}
      onPay={() => void pay()}
    />
  );

  const account = branch.current?.bankBin && branch.current.bankAccount ? branch.current : undefined;

  return (
    <>
      {/* Màn hình bán hàng không in; khi in chỉ còn phiếu thanh toán. */}
      <Flex vertical gap={12} className="no-print">
        {toastHolder}
        {modalHolder}
        <ScanModal
          open={scanning}
          onClose={() => setScanning(false)}
          afterClose={focusSearch}
          onCode={onScanCode}
          remote={sendToPc}
          onRemoteChange={changeSendToPc}
        />
        {branch.error && <Alert type="error" showIcon title={errorMessage(branch.error)} />}
        {search.error && <Alert type="error" showIcon title={errorMessage(search.error)} />}
        {!branch.branchId && !branch.error && <Alert type="info" showIcon title="Chọn chi nhánh bán hàng ở thanh trên trước khi bán." />}

        {wide ? (
          <Row gutter={16} align="top">
            <Col span={15}>{searchPanel}</Col>
            <Col span={9}>
              {/* Giỏ và thanh toán bám theo khi cuộn danh sách kết quả, nút thanh toán luôn trong tầm mắt. */}
              <Flex vertical gap={12} style={{ position: 'sticky', top: 80 }}>
                {cartPanel}
                <Card title="Thanh toán">{paymentPanel}</Card>
              </Flex>
            </Col>
          </Row>
        ) : (
          <>
            <Tabs
              items={[
                { key: 'search', label: 'Tìm hàng', children: searchPanel },
                {
                  key: 'cart',
                  label: (
                    <Badge count={cartCount(cart)} offset={[12, 0]}>
                      Giỏ hàng
                    </Badge>
                  ),
                  children: cartPanel,
                },
              ]}
              // Chừa chỗ cho thanh thanh toán cố định ở đáy; thanh cao hơn khi đang hiện mã QR và nút in phiếu.
              style={{ paddingBottom: method === 'QR' && cart.length > 0 ? 400 : 140 }}
            />
            <div
              style={{
                position: 'fixed',
                left: 0,
                right: 0,
                bottom: 0,
                padding: 12,
                background: token.colorBgContainer,
                borderTop: `1px solid ${token.colorBorder}`,
                boxShadow: '0 -4px 16px rgba(23, 32, 51, 0.08)',
                zIndex: 10,
              }}
            >
              {paymentPanel}
            </div>
          </>
        )}
      </Flex>
      {method === 'QR' && account && cart.length > 0 && (
        <div className="print-only">
          <PaymentSlip lines={cart} branchName={account.name} bankBin={account.bankBin!} bankAccount={account.bankAccount!} />
        </div>
      )}
    </>
  );
}
