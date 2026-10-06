import { ApiError, errorMessage, type PaymentMethod, type PosSku, type StockShortage } from '@oism/shared';
import { Alert, Badge, Col, Flex, Grid, type InputRef, message, Modal, Row, Select, Tabs } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { usePosBranch } from '../branch/usePosBranch';
import { addSku, applyShortages, type CartLine, cartCount, cartTotal, exactMatch, removeLine, setQuantity, toOrderLines } from '../cart/cart';
import { CartPanel } from '../cart/CartPanel';
import { PaymentPanel } from '../checkout/PaymentPanel';
import { useCheckout } from '../checkout/useCheckout';
import { SearchPanel } from '../search/SearchPanel';
import { usePosSearch } from '../search/usePosSearch';

// Màn hình bán hàng: tìm hoặc quét, giỏ, thanh toán trên một màn hình. Bố cục và phím tắt: docs/design/ui/pos.md.
export function SaleScreen() {
  const navigate = useNavigate();
  const wide = Grid.useBreakpoint().md;
  const [toast, toastHolder] = message.useMessage();
  const [modal, modalHolder] = Modal.useModal();

  const branch = usePosBranch();
  const [text, setText] = useState('');
  const [cart, setCart] = useState<CartLine[]>([]);
  const [selected, setSelected] = useState<string>();
  const [method, setMethod] = useState<PaymentMethod>('Cash');
  const search = usePosSearch(branch.branchId, text);
  const { checkout, pending } = useCheckout();

  // Hai lần quét liên tiếp có thể cùng đang chờ kết quả tìm: lần về sau phải cộng vào giỏ đã có lần về trước.
  const latestCart = useRef(cart);
  function updateCart(next: CartLine[]) {
    latestCart.current = next;
    setCart(next);
  }

  const searchInput = useRef<InputRef>(null);
  // Trên thiết bị cầm tay ô tìm không tự giành lại focus sau mỗi lần chạm, để bàn phím ảo không bật lên liên tục.
  const focusSearch = () => {
    if (wide) searchInput.current?.focus();
  };

  // typed: chữ đã quét ra SKU này; ô tìm chỉ được xóa nếu nó vẫn còn đúng chữ đó.
  function add(sku: PosSku, typed?: string) {
    const next = addSku(latestCart.current, sku);
    if (next === latestCart.current) toast.warning(sku.available > 0 ? `${sku.name}: chỉ còn ${sku.available}` : `${sku.name} đã hết hàng`);
    updateCart(next);
    setSelected(sku.skuId);
    setText((current) => (typed === undefined || current === typed ? '' : current));
    focusSearch();
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
  async function addScanned(typed: string) {
    try {
      const match = exactMatch(await search.searchNow(typed), typed);
      if (match) add(match, typed);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  }

  async function pay() {
    const lines = latestCart.current;
    if (!branch.branchId || lines.length === 0) return;
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
      disabled={!branch.branchId || cart.length === 0}
      pending={pending}
      showShortcuts={!!wide}
      onPay={() => void pay()}
    />
  );

  return (
    <Flex vertical gap={12}>
      {toastHolder}
      {modalHolder}
      {branch.canChoose && (
        <Select
          value={branch.branchId}
          options={branch.options}
          placeholder="Chọn chi nhánh bán hàng"
          aria-label="Chi nhánh bán hàng"
          style={{ maxWidth: 360 }}
          onChange={(branchId) => {
            // Giỏ thuộc về một chi nhánh: đổi chi nhánh thì bắt đầu giỏ mới.
            branch.choose(branchId);
            updateCart([]);
            focusSearch();
          }}
        />
      )}
      {branch.error && <Alert type="error" showIcon title={errorMessage(branch.error)} />}
      {search.error && <Alert type="error" showIcon title={errorMessage(search.error)} />}
      {!branch.branchId && !branch.error && <Alert type="info" showIcon title="Chọn chi nhánh trước khi bán hàng." />}

      {wide ? (
        <Row gutter={16}>
          <Col span={14}>{searchPanel}</Col>
          <Col span={10}>
            <Flex vertical gap={12}>
              {cartPanel}
              {paymentPanel}
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
            // Chừa chỗ cho thanh thanh toán cố định ở đáy; thanh cao hơn khi đang hiện mã QR.
            style={{ paddingBottom: method === 'QR' && cart.length > 0 ? 340 : 140 }}
          />
          <div
            style={{ position: 'fixed', left: 0, right: 0, bottom: 0, padding: 12, background: '#fff', boxShadow: '0 -2px 8px rgba(0,0,0,0.15)', zIndex: 10 }}
          >
            {paymentPanel}
          </div>
        </>
      )}
    </Flex>
  );
}
