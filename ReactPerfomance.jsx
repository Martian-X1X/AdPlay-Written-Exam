// Q14. REACT PERFORMANCE = display 1 million products efficiently

import React, {
  useState, useEffect, useCallback, useRef, useMemo
} from "react";
import { FixedSizeList as VirtualList } from "react-window"; // renders only visible rows, not all 1M


// Waits until the user stops typing for 400ms before doing anything.
// Stops us from firing an API call on every single keystroke.
function useDebounce(value, delay = 400) {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer); // if user types again before 400ms, cancel and restart
  }, [value, delay]);

  return debounced;
}


// One row in the list. Wrapped in React.memo so it only
// re-renders if ITS OWN data changed not every time the parent re-renders for an unrelated reason.
const ProductRow = React.memo(({ index, style, data }) => {
  const product = data[index];

  if (!product) {
    // Row hasn't loaded yet (we're still fetching it)
    return <div style={style} className="row loading">Loading...</div>;
  }

  return (
    // "style" comes from react-window it positions this row  on screen. Don't remove it or rows will overlap/break.
    <div style={style} className="row">
      <span>{product.name}</span>
      <span>${product.price}</span>
      <span>{product.brand}</span>
    </div>
  );
});


const PAGE_SIZE   = 50;  // how many products we ask for per API call
const ROW_HEIGHT  = 50;  // height of one row in pixels
const LIST_HEIGHT = 600; // height of the visible scroll area

export default function ProductList() {
  const [search,   setSearch]   = useState("");
  const [products, setProducts] = useState([]);
  const [hasMore,  setHasMore]  = useState(true);
  const [loading,  setLoading]  = useState(false);

  const debouncedSearch = useDebounce(search, 400);

  // Refs are used here instead of state for things onScroll needs
  // to read refs update INSTANTLY, state updates only on the
  // next render. Scrolling fires fast, so we need instant values
  // or we'd accidentally fetch the same page twice.
  const abortRef   = useRef(null); // lets us cancel an old API call
  const pageRef    = useRef(1);    // which page to fetch next
  const loadingRef = useRef(false);// are we already fetching right now?

  // Fetches one page of products from the API.
  const fetchProducts = useCallback(async (searchTerm, pageNum, reset = false) => {
    // If a previous call is still running, cancel it first
    // e.g. user types fast, we don't want old results to overwrite new ones.
    if (abortRef.current) abortRef.current.abort();
    abortRef.current = new AbortController();

    setLoading(true);
    loadingRef.current = true;

    try {
      const res = await fetch(
        `/api/products?keyword=${encodeURIComponent(searchTerm)}&page=${pageNum}&pageSize=${PAGE_SIZE}`,
        { signal: abortRef.current.signal }
      );
      const data = await res.json();

      setProducts(prev =>
        reset ? data.items : [...prev, ...data.items] // new search = replace, scroll = add on
      );

      // Got fewer items than we asked for = we've hit the end of the list
      setHasMore(data.items.length === PAGE_SIZE);
    } catch (err) {
      if (err.name === "AbortError") return; // we cancelled it ourselves, ignore
      console.error("Fetch failed:", err);
    } finally {
      setLoading(false);
      loadingRef.current = false;
    }
  }, []);

  // Whenever the search box changes (after the 400ms wait), start fresh.
  useEffect(() => {
    setProducts([]);
    setHasMore(true);
    pageRef.current = 1;
    fetchProducts(debouncedSearch, 1, true);
  }, [debouncedSearch, fetchProducts]);

  // Fires while scrolling. If we're near the bottom and there's
  // more data, load the next page.
  const onScroll = useCallback(({ scrollOffset }) => {
    const totalHeight  = products.length * ROW_HEIGHT;
    const scrollBottom = scrollOffset + LIST_HEIGHT;
    const nearBottom   = scrollBottom >= totalHeight - 200; // 200px before the end

    if (nearBottom && hasMore && !loadingRef.current) {
      const nextPage = pageRef.current + 1;
      pageRef.current = nextPage; // lock this in right away so a second scroll event can't sneak in and fetch the same page again
      fetchProducts(debouncedSearch, nextPage);
    }
  }, [products.length, hasMore, debouncedSearch, fetchProducts]);

  // How many "slots" the list should show. Adds 1 extra slot
  // for a loading placeholder while more pages are still coming.
  const itemCount = useMemo(
    () => hasMore ? products.length + 1 : products.length,
    [products.length, hasMore]
  );

  // If the component disappears (user navigates away) while a
  // fetch is still running, cancel it so it doesn't try to update
  // state that no longer exists.
  useEffect(() => {
    return () => {
      if (abortRef.current) abortRef.current.abort();
    };
  }, []);

  return (
    <div>
      <input
        placeholder="Search products..."
        value={search}
        onChange={e => setSearch(e.target.value)} // the debounce hook handles the delay
      />
      {loading && <span>Loading...</span>}

      {/*
        This is what makes 1 million rows possible: react-window
        only actually puts ~12 rows in the DOM at a time the
        ones currently visible. As you scroll, it swaps which
        rows are rendered. Without this, the browser would try
        to create 1 million <div>s and crash.
      */}
      <VirtualList
        height={LIST_HEIGHT}
        itemCount={itemCount}
        itemSize={ROW_HEIGHT}
        itemData={products}
        onScroll={onScroll}
        width="100%"
      >
        {ProductRow}
      </VirtualList>
    </div>
  );
}


// QUICK SUMMARY OF EACH TRICK USED
// Virtualization      = only render the rows you can actually see
// Infinite Scroll      = load more automatically as you near the bottom
// Debounced Search      = wait for typing to stop before calling the API
// Request Cancellation  = cancel old API calls when a new one starts
// Memoization           = avoid re-rendering/re-creating things that didn't change
