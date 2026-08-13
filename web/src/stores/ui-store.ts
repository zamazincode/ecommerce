import { create } from "zustand";

interface UiState {
	isCartPanelOpen: boolean;
	openCartPanel: () => void;
	closeCartPanel: () => void;
}

export const useUiStore = create<UiState>((set) => ({
	isCartPanelOpen: false,
	openCartPanel: () => set({ isCartPanelOpen: true }),
	closeCartPanel: () => set({ isCartPanelOpen: false }),
}));
