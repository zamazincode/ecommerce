import type { components } from "@/types/api";
import type { BadgeVariant } from "@/components/ui/badge";

type OrderStatus = components["schemas"]["OrderStatus"];
type CouponType = components["schemas"]["CouponType"];
type BookBinding = components["schemas"]["BookBinding"];

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
	0: "Beklemede",
	1: "Ödendi",
	2: "Hazırlanıyor",
	3: "Kargoya Verildi",
	4: "Teslim Edildi",
	5: "İptal Edildi",
	6: "İade Edildi",
};

export const ORDER_STATUS_TRANSITIONS: Record<OrderStatus, OrderStatus[]> = {
	0: [1, 5], // Pending → Paid, Cancelled
	1: [2, 5, 6], // Paid → Preparing, Cancelled, Refunded
	2: [3, 5], // Preparing → Shipped, Cancelled
	3: [4], // Shipped → Delivered
	4: [], // Delivered — terminal
	5: [], // Cancelled — terminal
	6: [], // Refunded — terminal
};

// Sipariş durumu rozetlerinin görsel tonu — Badge variant'larına eşleniyor.
export const ORDER_STATUS_TONES: Record<OrderStatus, BadgeVariant> = {
	0: "warning", // Beklemede
	1: "brand-soft", // Ödendi
	2: "brand-soft", // Hazırlanıyor
	3: "default", // Kargoya Verildi
	4: "success", // Teslim Edildi
	5: "destructive", // İptal Edildi
	6: "secondary", // İade Edildi
};

export const COUPON_TYPE_LABELS: Record<CouponType, string> = {
	0: "Yüzde",
	1: "Sabit Tutar",
};

// `api/src/Commerce.Domain/Common/Enums.cs` — Unknown/Paperback/Hardcover/Ebook.
export const BOOK_BINDING_LABELS: Record<BookBinding, string> = {
	0: "Belirtilmemiş",
	1: "Karton Kapak",
	2: "Ciltli",
	3: "E-Kitap",
};

// Denetim kaydı `AuditLog.Action` — backend'de serbest metin (enum değil),
// bu yüzden `Record` yerine haritalama + varsayılan geri dönüş kullanılıyor.
export const AUDIT_ACTION_LABELS: Record<string, string> = {
	Created: "Oluşturuldu",
	Updated: "Güncellendi",
	Deleted: "Silindi",
};

export const AUDIT_ACTION_TONES: Record<string, BadgeVariant> = {
	Created: "success",
	Updated: "brand-soft",
	Deleted: "destructive",
};
