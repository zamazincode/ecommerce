import { create } from "zustand";

interface UiState {
	isCartPanelOpen: boolean;
	openCartPanel: () => void;
	closeCartPanel: () => void;

	isMobileMenuOpen: boolean;
	openMobileMenu: () => void;
	closeMobileMenu: () => void;
	toggleMobileMenu: () => void;

	isMobileSearchOpen: boolean;
	openMobileSearch: () => void;
	closeMobileSearch: () => void;
}

export const useUiStore = create<UiState>((set) => ({
	isCartPanelOpen: false,
	openCartPanel: () => set({ isCartPanelOpen: true }),
	closeCartPanel: () => set({ isCartPanelOpen: false }),

	isMobileMenuOpen: false,
	openMobileMenu: () => set({ isMobileMenuOpen: true }),
	closeMobileMenu: () => set({ isMobileMenuOpen: false }),
	toggleMobileMenu: () =>
		set((s) => ({ isMobileMenuOpen: !s.isMobileMenuOpen })),

	isMobileSearchOpen: false,
	openMobileSearch: () => set({ isMobileSearchOpen: true }),
	closeMobileSearch: () => set({ isMobileSearchOpen: false }),
}));
